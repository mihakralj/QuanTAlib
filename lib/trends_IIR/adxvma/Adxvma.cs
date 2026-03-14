using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ADXVMA: ADX Variable Moving Average
/// </summary>
/// <remarks>
/// Adaptive IIR filter that uses ADX (Average Directional Index) as its smoothing constant.
/// When ADX is high (strong trend), the filter tracks price aggressively.
/// When ADX is low (range-bound), the filter barely moves.
///
/// Uses Wilder's RMA with warmup compensation for all internal components (TR, +DM, -DM, DX).
/// Requires OHLC data for TR/DM calculation; single-value input creates synthetic bars (TR=0).
///
/// Default period=14.
/// </remarks>
/// <seealso href="Adxvma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Adxvma : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct RmaState(double Ema, double E, bool IsCompensated);

    [StructLayout(LayoutKind.Auto)]
    private record struct AdxvmaState(
        RmaState Tr,
        RmaState Pdm,
        RmaState Ndm,
        RmaState Dx,
        double PrevHigh,
        double PrevLow,
        double PrevClose,
        double Result,
        bool IsInitialized,
        int BarCount)
    {
        public static AdxvmaState New() => new()
        {
            Tr = new RmaState(0, 1.0, false),
            Pdm = new RmaState(0, 1.0, false),
            Ndm = new RmaState(0, 1.0, false),
            Dx = new RmaState(0, 1.0, false),
            PrevHigh = double.NaN,
            PrevLow = double.NaN,
            PrevClose = double.NaN,
            Result = double.NaN,
            IsInitialized = false,
            BarCount = 0
        };
    }

    private readonly int _period;
    private readonly double _alpha;
    private readonly double _decay;
    private AdxvmaState _state;
    private AdxvmaState _p_state;
    private double _lastValidValue;
    private double _p_lastValidValue;

    private const double EPSILON = 1e-10;

    public override bool IsHot => _state.BarCount >= _period * 2;

    /// <summary>
    /// Creates ADXVMA with specified period.
    /// </summary>
    /// <param name="period">ADX calculation period (must be >= 1)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adxvma(int period = 14)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        _period = period;
        _alpha = 1.0 / period;
        _decay = 1.0 - _alpha;

        _state = AdxvmaState.New();
        _p_state = _state;
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;

        Name = $"Adxvma({_period})";
        WarmupPeriod = _period * 2;
    }

    /// <summary>
    /// Creates ADXVMA connected to a data source.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adxvma(ITValuePublisher source, int period = 14) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Updates ADXVMA with a TBar input (uses OHLC for TR/DM, Close for source).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state = _p_state;
            _lastValidValue = _p_lastValidValue;
        }

        double sourceValue = input.Close;
        if (!double.IsFinite(sourceValue))
        {
            sourceValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = sourceValue;
        }

        if (!double.IsFinite(sourceValue))
        {
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Calculate True Range
        double trueRange;
        if (!_state.IsInitialized || double.IsNaN(_state.PrevClose))
        {
            trueRange = input.High - input.Low;
        }
        else
        {
            double hl = input.High - input.Low;
            double hpc = Math.Abs(input.High - _state.PrevClose);
            double lpc = Math.Abs(input.Low - _state.PrevClose);
            trueRange = Math.Max(hl, Math.Max(hpc, lpc));
        }

        // Calculate Directional Movement
        double upMove = _state.IsInitialized && !double.IsNaN(_state.PrevHigh)
            ? input.High - _state.PrevHigh
            : 0.0;
        double downMove = _state.IsInitialized && !double.IsNaN(_state.PrevLow)
            ? _state.PrevLow - input.Low
            : 0.0;

        double plusDm = (upMove > downMove && upMove > 0) ? upMove : 0.0;
        double minusDm = (downMove > upMove && downMove > 0) ? downMove : 0.0;

        // Update RMAs with warmup compensation
        var tr = _state.Tr;
        var pdm = _state.Pdm;
        var ndm = _state.Ndm;

        tr.Ema = Math.FusedMultiplyAdd(tr.Ema, _decay, _alpha * trueRange);
        tr.E *= _decay;
        if (tr.E <= EPSILON)
        {
            tr.IsCompensated = true;
        }

        pdm.Ema = Math.FusedMultiplyAdd(pdm.Ema, _decay, _alpha * plusDm);
        pdm.E *= _decay;
        if (pdm.E <= EPSILON)
        {
            pdm.IsCompensated = true;
        }

        ndm.Ema = Math.FusedMultiplyAdd(ndm.Ema, _decay, _alpha * minusDm);
        ndm.E *= _decay;
        if (ndm.E <= EPSILON)
        {
            ndm.IsCompensated = true;
        }

        // Compensated values
        double compTr = tr.IsCompensated ? tr.Ema : tr.Ema / (1.0 - tr.E);
        double compPdm = pdm.IsCompensated ? pdm.Ema : pdm.Ema / (1.0 - pdm.E);
        double compNdm = ndm.IsCompensated ? ndm.Ema : ndm.Ema / (1.0 - ndm.E);

        // Calculate +DI, -DI, DX
        double plusDi = compTr > EPSILON ? 100.0 * compPdm / compTr : 0.0;
        double minusDi = compTr > EPSILON ? 100.0 * compNdm / compTr : 0.0;
        double diSum = plusDi + minusDi;
        double dx = diSum > EPSILON ? 100.0 * Math.Abs(plusDi - minusDi) / diSum : 0.0;

        // Smooth DX → ADX
        var dxState = _state.Dx;
        dxState.Ema = Math.FusedMultiplyAdd(dxState.Ema, _decay, _alpha * dx);
        dxState.E *= _decay;
        if (dxState.E <= EPSILON)
        {
            dxState.IsCompensated = true;
        }

        double adxVal = dxState.IsCompensated ? dxState.Ema : dxState.Ema / (1.0 - dxState.E);

        // Smoothing constant from ADX
        double sc = Math.Max(0.0, Math.Min(adxVal / 100.0, 1.0));

        // Adaptive EMA
        double result = double.IsNaN(_state.Result) ? sourceValue : _state.Result + (sc * (sourceValue - _state.Result));

        // Update state
        _state = new AdxvmaState(
            tr, pdm, ndm, dxState,
            input.High, input.Low, input.Close,
            result,
            IsInitialized: true,
            BarCount: _state.BarCount + (isNew ? 1 : 0));

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates ADXVMA with a TValue input.
    /// Creates synthetic bar with O=H=L=C (TR=0, no directional movement).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        var syntheticBar = new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0);
        return Update(syntheticBar, isNew);
    }

    /// <summary>
    /// Updates ADXVMA with a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            var result = Update(bar, isNew: true);
            tSpan[i] = bar.Time;
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates ADXVMA with a TSeries (single values).
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        var sourceTimes = source.Times;
        var sourceValues = source.Values;

        for (int i = 0; i < len; i++)
        {
            var result = Update(new TValue(sourceTimes[i], sourceValues[i]), isNew: true);
            tSpan[i] = sourceTimes[i];
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();
        foreach (double val in source)
        {
            Update(new TValue(DateTime.MinValue, val), isNew: true);
        }
    }

    /// <summary>
    /// Calculates ADXVMA from a TBarSeries.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        var adxvma = new Adxvma(period);
        return adxvma.Update(source);
    }

    /// <summary>
    /// Calculates ADXVMA from a TSeries (single values; TR=0).
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 14)
    {
        var adxvma = new Adxvma(period);
        return adxvma.Update(source);
    }

    /// <summary>
    /// Creates an ADXVMA indicator and calculates results from a TBarSeries.
    /// </summary>
    public static (TSeries Results, Adxvma Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Adxvma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Creates an ADXVMA indicator and calculates results from a TSeries.
    /// </summary>
    public static (TSeries Results, Adxvma Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Adxvma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = AdxvmaState.New();
        _p_state = _state;
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        Last = default;
    }
}
