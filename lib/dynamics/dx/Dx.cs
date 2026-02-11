using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// DX: Directional Movement Index
/// </summary>
/// <remarks>
/// Unsmoothed trend strength indicator [0-100] regardless of direction (Wilder).
/// Unlike ADX, DX is not smoothed - it shows raw directional movement strength.
/// Values above 25 indicate strong trend. DX is the building block for ADX.
///
/// Calculation: <c>DX = |+DI - -DI| / (+DI + -DI) × 100</c> where DI values use RMA-smoothed +DM/-DM/TR.
/// </remarks>
/// <seealso href="Dx.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Dx : ITValuePublisher
{
    private readonly int _period;
    private readonly double _invPeriod;  // 1 / period
    private TBar _prevBar;
    private TBar _p_prevBar;
    private bool _isInitialized;

    // State for TR, +DM, -DM smoothing
    private double _trSum, _dmPlusSum, _dmMinusSum;
    private double _p_trSum, _p_dmPlusSum, _p_dmMinusSum;
    private int _samples;
    private int _p_samples;

    private double _trSmooth, _dmPlusSmooth, _dmMinusSmooth;
    private double _p_trSmooth, _p_dmPlusSmooth, _p_dmMinusSmooth;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current DX value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current +DI value.
    /// </summary>
    public TValue DiPlus { get; private set; }

    /// <summary>
    /// Current -DI value.
    /// </summary>
    public TValue DiMinus { get; private set; }

    /// <summary>
    /// True if the DX has warmed up and is providing valid results.
    /// </summary>
    public bool IsHot => _samples >= _period;

    /// <summary>
    /// The period parameter.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates DX with specified period.
    /// </summary>
    /// <param name="period">Period for DX calculation (must be > 0)</param>
    public Dx(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _invPeriod = 1.0 / period;
        Name = $"DX({period})";
        WarmupPeriod = period;
        _isInitialized = false;
    }

    /// <summary>
    /// Resets the DX state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _prevBar = default;
        _p_prevBar = default;
        _isInitialized = false;

        _trSum = _dmPlusSum = _dmMinusSum = 0;
        _p_trSum = _p_dmPlusSum = _p_dmMinusSum = 0;
        _samples = _p_samples = 0;

        _trSmooth = _dmPlusSmooth = _dmMinusSmooth = 0;
        _p_trSmooth = _p_dmPlusSmooth = _p_dmMinusSmooth = 0;

        Last = default;
        DiPlus = default;
        DiMinus = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_prevBar = _prevBar;
            _p_trSum = _trSum;
            _p_dmPlusSum = _dmPlusSum;
            _p_dmMinusSum = _dmMinusSum;
            _p_samples = _samples;
            _p_trSmooth = _trSmooth;
            _p_dmPlusSmooth = _dmPlusSmooth;
            _p_dmMinusSmooth = _dmMinusSmooth;
        }
        else
        {
            _prevBar = _p_prevBar;
            _trSum = _p_trSum;
            _dmPlusSum = _p_dmPlusSum;
            _dmMinusSum = _p_dmMinusSum;
            _samples = _p_samples;
            _trSmooth = _p_trSmooth;
            _dmPlusSmooth = _p_dmPlusSmooth;
            _dmMinusSmooth = _p_dmMinusSmooth;
        }

        if (!_isInitialized)
        {
            if (isNew)
            {
                _prevBar = input;
                _isInitialized = true;
            }
            return new TValue(input.Time, 0);
        }

        // Calculate TR with NaN/Infinity guards
        double high = double.IsFinite(input.High) ? input.High : _prevBar.High;
        double low = double.IsFinite(input.Low) ? input.Low : _prevBar.Low;
        double prevClose = double.IsFinite(_prevBar.Close) ? _prevBar.Close : high;
        double prevHigh = double.IsFinite(_prevBar.High) ? _prevBar.High : high;
        double prevLow = double.IsFinite(_prevBar.Low) ? _prevBar.Low : low;

        double hl = high - low;
        double hpc = Math.Abs(high - prevClose);
        double lpc = Math.Abs(low - prevClose);
        double tr = Math.Max(hl, Math.Max(hpc, lpc));

        // Guard TR against non-finite values
        if (!double.IsFinite(tr))
        {
            tr = 0;
        }

        // Calculate DM using guarded values
        double dmPlus = 0;
        double dmMinus = 0;
        double upMove = high - prevHigh;
        double downMove = prevLow - low;

        // Guard moves against non-finite values
        if (!double.IsFinite(upMove))
        {
            upMove = 0;
        }

        if (!double.IsFinite(downMove))
        {
            downMove = 0;
        }

        if (upMove > downMove && upMove > 0)
        {
            dmPlus = upMove;
        }

        if (downMove > upMove && downMove > 0)
        {
            dmMinus = downMove;
        }

        if (isNew)
        {
            // Store sanitized values to prevent NaN/Infinity propagation to next bar
            double close = double.IsFinite(input.Close) ? input.Close : prevClose;
            _prevBar = new TBar(input.Time, high, high, low, close, input.Volume);
        }

        // Smooth TR, +DM, -DM
        if (_samples < _period)
        {
            _trSum += tr;
            _dmPlusSum += dmPlus;
            _dmMinusSum += dmMinus;
            _samples++;

            if (_samples == _period)
            {
                // Wilder's initialization for TR, +DM, and -DM uses the un-averaged sum (scaled sum).
                _trSmooth = _trSum;
                _dmPlusSmooth = _dmPlusSum;
                _dmMinusSmooth = _dmMinusSum;
            }
        }
        else
        {
            // Wilder's smoothing: Smooth = Smooth - Smooth/N + Input
            // This is different from RMA: Smooth = Smooth * (N-1)/N + Input/N
            _trSmooth = _trSmooth - (_trSmooth * _invPeriod) + tr;
            _dmPlusSmooth = _dmPlusSmooth - (_dmPlusSmooth * _invPeriod) + dmPlus;
            _dmMinusSmooth = _dmMinusSmooth - (_dmMinusSmooth * _invPeriod) + dmMinus;
        }

        // Calculate DI and DX
        double diPlus = 0;
        double diMinus = 0;
        double dx = 0;

        if (_samples >= _period)
        {
            if (_trSmooth > 1e-10)
            {
                diPlus = (_dmPlusSmooth / _trSmooth) * 100.0;
                diMinus = (_dmMinusSmooth / _trSmooth) * 100.0;
            }

            // Guard against NaN/Infinity in DI calculations
            if (!double.IsFinite(diPlus))
            {
                diPlus = 0;
            }

            if (!double.IsFinite(diMinus))
            {
                diMinus = 0;
            }

            double diSum = diPlus + diMinus;
            if (diSum > 1e-10)
            {
                dx = (Math.Abs(diPlus - diMinus) / diSum) * 100.0;
            }

            // Guard against NaN/Infinity in DX calculation
            if (!double.IsFinite(dx))
            {
                dx = 0;
            }
        }

        // Ensure all outputs are finite; if not, use previous values or 0
        if (!double.IsFinite(diPlus))
        {
            diPlus = double.IsFinite(DiPlus.Value) ? DiPlus.Value : 0;
        }

        if (!double.IsFinite(diMinus))
        {
            diMinus = double.IsFinite(DiMinus.Value) ? DiMinus.Value : 0;
        }

        if (!double.IsFinite(dx))
        {
            dx = double.IsFinite(Last.Value) ? Last.Value : 0;
        }

        DiPlus = new TValue(input.Time, diPlus);
        DiMinus = new TValue(input.Time, diMinus);
        Last = new TValue(input.Time, dx);

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        var len = source.Count;
        var v = new double[len];

        // Use the static Calculate method for performance
        Batch(source.High.Values, source.Low.Values, source.Close.Values, _period, v);

        // Create lists for TSeries
        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        // Restore state by replaying the whole series
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(tList, [.. v]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalcTrDm(int i, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, out double tr, out double dmPlus, out double dmMinus)
    {
        double h = high[i];
        double l = low[i];
        double pc = close[i - 1];
        double ph = high[i - 1];
        double pl = low[i - 1];

        double hl = h - l;
        double hpc = Math.Abs(h - pc);
        double lpc = Math.Abs(l - pc);
        tr = Math.Max(hl, Math.Max(hpc, lpc));

        double up = h - ph;
        double down = pl - l;
        dmPlus = (up > down && up > 0) ? up : 0;
        dmMinus = (down > up && down > 0) ? down : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalcDx(double trSmooth, double dmPlusSmooth, double dmMinusSmooth)
    {
        double diPlus = (trSmooth > 1e-10) ? (dmPlusSmooth / trSmooth) * 100.0 : 0;
        double diMinus = (trSmooth > 1e-10) ? (dmMinusSmooth / trSmooth) * 100.0 : 0;
        double diSum = diPlus + diMinus;
        return (diSum > 1e-10) ? (Math.Abs(diPlus - diMinus) / diSum) * 100.0 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WilderSmooth(double input, double invPeriod, ref double smoothed)
    {
        // Wilder's smoothing: Smooth = Smooth - Smooth/N + Input
        smoothed = smoothed - (smoothed * invPeriod) + input;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, int period, Span<double> destination)
    {
        int len = high.Length;
        if (len < period + 1)
        {
            destination.Clear();
            return;
        }

        double invPeriod = 1.0 / period;

        // Initialize with zeros
        for (int i = 0; i <= period; i++)
        {
            destination[i] = 0;
        }

        // Phase 1: Accumulate TR, +DM, -DM for the first 'period' bars
        double trSum = 0;
        double dmPlusSum = 0;
        double dmMinusSum = 0;

        for (int i = 1; i <= period; i++)
        {
            CalcTrDm(i, high, low, close, out double tr, out double dmPlus, out double dmMinus);
            trSum += tr;
            dmPlusSum += dmPlus;
            dmMinusSum += dmMinus;
        }

        // Initialize smoothed values
        double trSmooth = trSum;
        double dmPlusSmooth = dmPlusSum;
        double dmMinusSmooth = dmMinusSum;

        // Calculate DX at period index
        double dx = CalcDx(trSmooth, dmPlusSmooth, dmMinusSmooth);
        destination[period] = dx;

        // Phase 2: Calculate DX for the rest of the series
        for (int i = period + 1; i < len; i++)
        {
            CalcTrDm(i, high, low, close, out double tr, out double dmPlus, out double dmMinus);

            WilderSmooth(tr, invPeriod, ref trSmooth);
            WilderSmooth(dmPlus, invPeriod, ref dmPlusSmooth);
            WilderSmooth(dmMinus, invPeriod, ref dmMinusSmooth);

            dx = CalcDx(trSmooth, dmPlusSmooth, dmMinusSmooth);
            destination[i] = dx;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        var len = source.Count;
        var v = new double[len];
        Batch(source.High.Values, source.Low.Values, source.Close.Values, period, v);

        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        return new TSeries(tList, [.. v]);
    }

    public static (TSeries Results, Dx Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Dx(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
