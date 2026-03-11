using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ADX: Average Directional Index
/// </summary>
/// <remarks>
/// Trend strength indicator [0-100] regardless of direction (Wilder).
/// Derived from smoothed DX using +DI/-DI relationship. Values above 25 indicate strong trend.
///
/// Calculation: <c>ADX = RMA(DX)</c> where <c>DX = |+DI - -DI| / (+DI + -DI) × 100</c>.
/// </remarks>
/// <seealso href="Adx.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Adx : ITValuePublisher
{
    private readonly int _period;
    private readonly double _decay;      // (period - 1) / period for RMA
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

    // State for ADX smoothing
    private double _dxSum;
    private double _p_dxSum;
    private int _dxSamples;
    private int _p_dxSamples;

    private double _adx;
    private double _p_adx;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current ADX value.
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
    /// Current smoothed +DM value (RMA-smoothed raw plus directional movement, before TR normalization).
    /// </summary>
    public TValue DmPlus { get; private set; }

    /// <summary>
    /// Current smoothed -DM value (RMA-smoothed raw minus directional movement, before TR normalization).
    /// </summary>
    public TValue DmMinus { get; private set; }

    /// <summary>
    /// True if the ADX has warmed up and is providing valid results.
    /// </summary>
    public bool IsHot => _dxSamples >= _period;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates ADX with specified period.
    /// </summary>
    /// <param name="period">Period for ADX calculation (must be > 0)</param>
    public Adx(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _decay = (period - 1.0) / period;
        _invPeriod = 1.0 / period;
        Name = $"Adx({period})";
        WarmupPeriod = period * 2; // Needs period for TR/DM smoothing, then period for ADX smoothing
        _isInitialized = false;
    }

    /// <summary>
    /// Resets the ADX state.
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

        _dxSum = _p_dxSum = 0;
        _dxSamples = _p_dxSamples = 0;

        _adx = _p_adx = 0;

        Last = default;
        DiPlus = default;
        DiMinus = default;
        DmPlus = default;
        DmMinus = default;
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
            _p_dxSum = _dxSum;
            _p_dxSamples = _dxSamples;
            _p_adx = _adx;
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
            _dxSum = _p_dxSum;
            _dxSamples = _p_dxSamples;
            _adx = _p_adx;
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
                // Since +DI and -DI are ratios (+DM/TR and -DM/TR), the scaling factor (1/Period)
                // cancels out mathematically. This differs from the ADX smoothing later, which
                // explicitly uses a true SMA (sum / Period) for its initialization.
                _trSmooth = _trSum;
                _dmPlusSmooth = _dmPlusSum;
                _dmMinusSmooth = _dmMinusSum;
            }
        }
        else
        {
            // RMA: Smooth = Smooth * decay + Input * invPeriod
            // Using FMA for precision
            _trSmooth = Math.FusedMultiplyAdd(_trSmooth, _decay, tr * _invPeriod);
            _dmPlusSmooth = Math.FusedMultiplyAdd(_dmPlusSmooth, _decay, dmPlus * _invPeriod);
            _dmMinusSmooth = Math.FusedMultiplyAdd(_dmMinusSmooth, _decay, dmMinus * _invPeriod);
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

            // Smooth DX to get ADX
            if (_dxSamples < _period)
            {
                _dxSum += dx;
                _dxSamples++;

                if (_dxSamples == _period)
                {
                    _adx = _dxSum * _invPeriod; // First ADX is SMA of DX
                }
            }
            else
            {
                // ADX = Prior ADX * decay + DX * invPeriod (RMA smoothing)
                _adx = Math.FusedMultiplyAdd(_adx, _decay, dx * _invPeriod);
            }

            // Final guard on ADX
            if (!double.IsFinite(_adx))
            {
                _adx = _p_adx;
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

        // Final guard on ADX output - ensure we always return a finite value
        double finalAdx = _adx;
        if (!double.IsFinite(finalAdx))
        {
            finalAdx = _p_adx;
        }

        if (!double.IsFinite(finalAdx))
        {
            finalAdx = 0;
        }

        DiPlus = new TValue(input.Time, diPlus);
        DiMinus = new TValue(input.Time, diMinus);
        DmPlus = new TValue(input.Time, _samples >= _period ? _dmPlusSmooth : 0);
        DmMinus = new TValue(input.Time, _samples >= _period ? _dmMinusSmooth : 0);
        Last = new TValue(input.Time, finalAdx);

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

        // Create lists for TSeries - use collection expression directly
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
    private static void Smooth(double input, double decay, double invPeriod, ref double smoothed)
    {
        // RMA: smoothed = smoothed * decay + input * invPeriod
        smoothed = Math.FusedMultiplyAdd(smoothed, decay, input * invPeriod);
    }

    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    /// <param name="source">Historical bar data.</param>
    public void Prime(TBarSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, int period, Span<double> destination)
    {
        int len = high.Length;
        if (len < period * 2)
        {
            destination.Clear();
            return;
        }

        double decay = (period - 1.0) / period;
        double invPeriod = 1.0 / period;

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
            destination[i] = 0;
        }
        destination[0] = 0;

        // Initialize smoothed values
        double trSmooth = trSum;
        double dmPlusSmooth = dmPlusSum;
        double dmMinusSmooth = dmMinusSum;

        // Phase 2: Calculate DX and accumulate it for ADX initialization
        double dxSum = 0;

        // Calculate DX for the 'period' index (first valid DX)
        double dx = CalcDx(trSmooth, dmPlusSmooth, dmMinusSmooth);
        dxSum += dx;

        int adxStart = (period * 2) - 1;

        for (int i = period + 1; i <= adxStart; i++)
        {
            CalcTrDm(i, high, low, close, out double tr, out double dmPlus, out double dmMinus);

            Smooth(tr, decay, invPeriod, ref trSmooth);
            Smooth(dmPlus, decay, invPeriod, ref dmPlusSmooth);
            Smooth(dmMinus, decay, invPeriod, ref dmMinusSmooth);

            dx = CalcDx(trSmooth, dmPlusSmooth, dmMinusSmooth);
            dxSum += dx;
            destination[i] = 0;
        }

        // Initialize ADX (SMA of DX)
        double adx = dxSum * invPeriod;
        destination[adxStart] = adx;

        // Phase 3: Calculate ADX for the rest of the series
        for (int i = adxStart + 1; i < len; i++)
        {
            CalcTrDm(i, high, low, close, out double tr, out double dmPlus, out double dmMinus);

            Smooth(tr, decay, invPeriod, ref trSmooth);
            Smooth(dmPlus, decay, invPeriod, ref dmPlusSmooth);
            Smooth(dmMinus, decay, invPeriod, ref dmMinusSmooth);

            dx = CalcDx(trSmooth, dmPlusSmooth, dmMinusSmooth);

            // ADX Smoothing (RMA)
            Smooth(dx, decay, invPeriod, ref adx);
            destination[i] = adx;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSeries Batch(TBarSeries source, int period)
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

    public static (TSeries Results, Adx Indicator) Calculate(TBarSeries source, int period)
    {
        var indicator = new Adx(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
