using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// VORTEX: Vortex Indicator
/// </summary>
/// <remarks>
/// Trend indicator using vortex movements and true range (Botes &amp; Siepman 2010).
/// VI+ measures positive vortex movement, VI- measures negative vortex movement.
/// Crossovers signal trend changes: VI+ crossing above VI- indicates bullish trend.
///
/// Calculation: <c>VI+ = Sum(VM+, N) / Sum(TR, N)</c>; <c>VI- = Sum(VM-, N) / Sum(TR, N)</c>
/// where VM+ = |High - Low[1]|, VM- = |Low - High[1]|, TR = True Range.
/// </remarks>
/// <seealso href="Vortex.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Vortex : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _vmPlusBuffer;
    private readonly RingBuffer _vmMinusBuffer;
    private readonly RingBuffer _trBuffer;
    private TBar _prevBar;
    private TBar _p_prevBar;
    private bool _isInitialized;

    // Running sums for O(1) updates
    private double _sumVmPlus, _sumVmMinus, _sumTr;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current VI+ value (Positive Vortex Indicator).
    /// This is also the Last value for convenience.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current VI+ value (Positive Vortex Indicator).
    /// </summary>
    public TValue ViPlus { get; private set; }

    /// <summary>
    /// Current VI- value (Negative Vortex Indicator).
    /// </summary>
    public TValue ViMinus { get; private set; }

    /// <summary>
    /// True if the indicator has enough data for a full period calculation.
    /// </summary>
    public bool IsHot => _vmPlusBuffer.IsFull;

    /// <summary>
    /// The period parameter.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates Vortex indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period for summing (must be > 1, default 14)</param>
    public Vortex(int period = 14)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _period = period;
        Name = $"Vortex({period})";
        WarmupPeriod = period;
        _vmPlusBuffer = new RingBuffer(period);
        _vmMinusBuffer = new RingBuffer(period);
        _trBuffer = new RingBuffer(period);
        _isInitialized = false;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _prevBar = default;
        _p_prevBar = default;
        _isInitialized = false;
        _vmPlusBuffer.Clear();
        _vmMinusBuffer.Clear();
        _trBuffer.Clear();
        _sumVmPlus = _sumVmMinus = _sumTr = 0;
        Last = default;
        ViPlus = default;
        ViMinus = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (!_isInitialized)
        {
            _prevBar = input;
            _p_prevBar = input;
            _isInitialized = true;
            Last = new TValue(input.Time, 0);
            ViPlus = Last;
            ViMinus = new TValue(input.Time, 0);
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
            return Last;
        }

        // Bar correction: restore previous state and recalculate sums from buffer
        if (!isNew)
        {
            _prevBar = _p_prevBar;
            // Recalculate sums from buffer contents (excluding the newest that will be replaced)
            _sumVmPlus = _vmPlusBuffer.Sum - _vmPlusBuffer.Newest;
            _sumVmMinus = _vmMinusBuffer.Sum - _vmMinusBuffer.Newest;
            _sumTr = _trBuffer.Sum - _trBuffer.Newest;
        }
        else
        {
            // Save state for potential correction
            _p_prevBar = _prevBar;
        }

        // Calculate values with NaN/Infinity guards
        double high = double.IsFinite(input.High) ? input.High : _prevBar.High;
        double low = double.IsFinite(input.Low) ? input.Low : _prevBar.Low;
        double prevHigh = double.IsFinite(_prevBar.High) ? _prevBar.High : high;
        double prevLow = double.IsFinite(_prevBar.Low) ? _prevBar.Low : low;
        double prevClose = double.IsFinite(_prevBar.Close) ? _prevBar.Close : high;

        // VM+ = |High - Low[1]|
        double vmPlus = Math.Abs(high - prevLow);

        // VM- = |Low - High[1]|
        double vmMinus = Math.Abs(low - prevHigh);

        // True Range = max(High - Low, |High - Close[1]|, |Low - Close[1]|)
        double tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));

        // For isNew=true with full buffer, subtract oldest before adding
        if (isNew && _vmPlusBuffer.IsFull)
        {
            _sumVmPlus -= _vmPlusBuffer.Oldest;
            _sumVmMinus -= _vmMinusBuffer.Oldest;
            _sumTr -= _trBuffer.Oldest;
        }

        // Add new values to buffers
        _vmPlusBuffer.Add(vmPlus, isNew);
        _vmMinusBuffer.Add(vmMinus, isNew);
        _trBuffer.Add(tr, isNew);

        // Update sums
        _sumVmPlus += vmPlus;
        _sumVmMinus += vmMinus;
        _sumTr += tr;

        // Calculate VI+ and VI- only when buffer is full
        double viPlus = 0;
        double viMinus = 0;
        if (_vmPlusBuffer.IsFull && _sumTr > 0)
        {
            viPlus = _sumVmPlus / _sumTr;
            viMinus = _sumVmMinus / _sumTr;
        }

        if (isNew)
        {
            _prevBar = input;
        }

        ViPlus = new TValue(input.Time, viPlus);
        ViMinus = new TValue(input.Time, viMinus);
        Last = ViPlus;  // VI+ is the primary output

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

        int len = source.Count;
        var viPlusValues = new double[len];
        var viMinusValues = new double[len];

        Batch(source.High.Values, source.Low.Values, source.Close.Values, _period, viPlusValues, viMinusValues);

        var tList = new List<long>(len);
        var vList = new List<double>(viPlusValues);

        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Calculates Vortex indicator values using O(n) sliding window algorithm.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="close">Close prices</param>
    /// <param name="period">Lookback period</param>
    /// <param name="viPlus">Output VI+ values</param>
    /// <param name="viMinus">Output VI- values</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
                                 int period, Span<double> viPlus, Span<double> viMinus)
    {
        int len = high.Length;
        if (len == 0 || len != low.Length || len != close.Length || len != viPlus.Length || len != viMinus.Length || period <= 1)
        {
            return;
        }

        // First bar - no previous bar available
        viPlus[0] = 0;
        viMinus[0] = 0;

        if (len < 2)
        {
            return;
        }

        // Calculate individual VM+, VM-, TR values
        Span<double> vmPlusValues = stackalloc double[len];
        Span<double> vmMinusValues = stackalloc double[len];
        Span<double> trValues = stackalloc double[len];

        vmPlusValues[0] = 0;
        vmMinusValues[0] = 0;
        trValues[0] = high[0] - low[0];

        for (int i = 1; i < len; i++)
        {
            vmPlusValues[i] = Math.Abs(high[i] - low[i - 1]);
            vmMinusValues[i] = Math.Abs(low[i] - high[i - 1]);
            trValues[i] = Math.Max(high[i] - low[i], Math.Max(Math.Abs(high[i] - close[i - 1]), Math.Abs(low[i] - close[i - 1])));
        }

        // Calculate running sums
        double sumVmPlus = 0, sumVmMinus = 0, sumTr = 0;

        for (int i = 1; i < len; i++)
        {
            // Add current values
            sumVmPlus += vmPlusValues[i];
            sumVmMinus += vmMinusValues[i];
            sumTr += trValues[i];

            // Remove oldest if past period
            if (i > period)
            {
                sumVmPlus -= vmPlusValues[i - period];
                sumVmMinus -= vmMinusValues[i - period];
                sumTr -= trValues[i - period];
            }

            // Calculate ratios
            if (i >= period && sumTr > 0)
            {
                viPlus[i] = sumVmPlus / sumTr;
                viMinus[i] = sumVmMinus / sumTr;
            }
            else
            {
                viPlus[i] = 0;
                viMinus[i] = 0;
            }
        }
    }

    public static TSeries Batch(TBarSeries source)
    {
        return Batch(source, 14);
    }

    public static TSeries Batch(TBarSeries source, int period)
    {
        var vortex = new Vortex(period);
        return vortex.Update(source);
    }

    public static (TSeries Results, Vortex Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Vortex();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

}
