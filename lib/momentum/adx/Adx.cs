using System.Runtime.CompilerServices;
using System.Buffers;

namespace QuanTAlib;

/// <summary>
/// ADX: Average Directional Index
/// </summary>
/// <remarks>
/// ADX measures the strength of a trend, regardless of its direction.
/// It is derived from the Smoothed Directional Movement Index (DX).
///
/// Calculation:
/// 1. Calculate True Range (TR), +DM, and -DM
/// 2. Smooth TR, +DM, -DM using RMA (Wilder's Moving Average)
///    - First value is SMA of first Period values
///    - Subsequent values: Previous + (Input - Previous) / Period
/// 3. Calculate +DI = (+DM_smooth / TR_smooth) * 100
/// 4. Calculate -DI = (-DM_smooth / TR_smooth) * 100
/// 5. Calculate DX = |(+DI - -DI) / (+DI + -DI)| * 100
/// 6. ADX = RMA(DX)
///    - First value is SMA of first Period DX values
///    - Subsequent values: Previous + (Input - Previous) / Period
///
/// Sources:
/// https://www.investopedia.com/terms/a/adx.asp
/// "New Concepts in Technical Trading Systems" by J. Welles Wilder
/// </remarks>
[SkipLocalsInit]
public sealed class Adx : ITValuePublisher
{
    private readonly int _period;
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

    public event Action<TValue>? Pub;

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
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
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

        // Calculate TR
        double hl = input.High - input.Low;
        double hpc = Math.Abs(input.High - _prevBar.Close);
        double lpc = Math.Abs(input.Low - _prevBar.Close);
        double tr = Math.Max(hl, Math.Max(hpc, lpc));

        // Calculate DM
        double dmPlus = 0;
        double dmMinus = 0;
        double upMove = input.High - _prevBar.High;
        double downMove = _prevBar.Low - input.Low;

        if (upMove > downMove && upMove > 0)
            dmPlus = upMove;

        if (downMove > upMove && downMove > 0)
            dmMinus = downMove;

        if (isNew)
        {
            _prevBar = input;
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
            // RMA: Previous + (Input - Previous) / Period
            // Or: Previous * (1 - 1/Period) + Input * (1/Period)
            // Or: (Previous * (Period - 1) + Input) / Period
            // Wilder uses sums, but effectively it's RMA.
            // Standard formula:
            // Smooth = Smooth - (Smooth / Period) + Input

            _trSmooth = _trSmooth - (_trSmooth / _period) + tr;
            _dmPlusSmooth = _dmPlusSmooth - (_dmPlusSmooth / _period) + dmPlus;
            _dmMinusSmooth = _dmMinusSmooth - (_dmMinusSmooth / _period) + dmMinus;
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

            double diSum = diPlus + diMinus;
            if (diSum > 1e-10)
            {
                dx = (Math.Abs(diPlus - diMinus) / diSum) * 100.0;
            }

            // Smooth DX to get ADX
            if (_dxSamples < _period)
            {
                _dxSum += dx;
                _dxSamples++;

                if (_dxSamples == _period)
                {
                    _adx = _dxSum / _period; // First ADX is SMA of DX
                }
            }
            else
            {
                // ADX = (Prior ADX * (Period - 1) + Current DX) / Period
                _adx = ((_adx * (_period - 1)) + dx) / _period;
            }
        }

        DiPlus = new TValue(input.Time, diPlus);
        DiMinus = new TValue(input.Time, diMinus);
        Last = new TValue(input.Time, _adx);

        Pub?.Invoke(Last);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        var len = source.Count;
        var v = new double[len];

        // Use the static Calculate method for performance
        Calculate(source.Open.Values, source.High.Values, source.Low.Values, source.Close.Values, _period, v);

        // Create lists for TSeries
        var tList = new List<long>(len);
        for (int i = 0; i < len; i++)
        {
            tList.Add(source.Open.Times[i]);
        }
        var vList = new List<double>(v);

        // Copy timestamps
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        // Restore state by replaying the whole series
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], true);
        }

        return new TSeries(tList, vList);
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
    private static void Smooth(double input, int period, ref double smoothed)
    {
        smoothed = smoothed - (smoothed / period) + input;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, int period, Span<double> destination)
    {
        int len = high.Length;
        if (len < period * 2)
        {
            destination.Clear();
            return;
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

        int adxStart = period * 2 - 1;

        for (int i = period + 1; i <= adxStart; i++)
        {
            CalcTrDm(i, high, low, close, out double tr, out double dmPlus, out double dmMinus);

            Smooth(tr, period, ref trSmooth);
            Smooth(dmPlus, period, ref dmPlusSmooth);
            Smooth(dmMinus, period, ref dmMinusSmooth);

            dx = CalcDx(trSmooth, dmPlusSmooth, dmMinusSmooth);
            dxSum += dx;
            destination[i] = 0;
        }

        // Initialize ADX (SMA of DX)
        double adx = dxSum / period;
        destination[adxStart] = adx;

        // Phase 3: Calculate ADX for the rest of the series
        for (int i = adxStart + 1; i < len; i++)
        {
            CalcTrDm(i, high, low, close, out double tr, out double dmPlus, out double dmMinus);

            Smooth(tr, period, ref trSmooth);
            Smooth(dmPlus, period, ref dmPlusSmooth);
            Smooth(dmMinus, period, ref dmMinusSmooth);

            dx = CalcDx(trSmooth, dmPlusSmooth, dmMinusSmooth);

            // ADX Smoothing (RMA)
            Smooth(dx / period, period, ref adx);
            destination[i] = adx;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSeries Batch(TBarSeries source, int period)
    {
        if (source.Count == 0) return new TSeries([], []);
        var len = source.Count;
        var v = new double[len];
        Calculate(source.Open.Values, source.High.Values, source.Low.Values, source.Close.Values, period, v);

        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        return new TSeries(tList, new List<double>(v));
    }
}
