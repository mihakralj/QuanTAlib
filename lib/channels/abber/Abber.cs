using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Abber: Aberration Bands
/// </summary>
/// <remarks>
/// Aberration Bands measure price deviation from a central moving average using absolute
/// deviation rather than standard deviation. This approach provides more intuitive and
/// outlier-resistant bands compared to Bollinger Bands.
///
/// Calculation:
/// Middle Band = SMA(Source, Period)
/// Deviation = |Source - Middle|
/// Average Deviation = SMA(Deviation, Period)
/// Upper Band = Middle + (Multiplier x Average Deviation)
/// Lower Band = Middle - (Multiplier x Average Deviation)
///
/// Key characteristics:
/// - Uses absolute deviation instead of standard deviation
/// - Less sensitive to extreme outliers than Bollinger Bands
/// - Provides intuitive measure of typical price dispersion
/// - Bands expand during volatile periods and contract during consolidation
///
/// Sources:
/// Pine Script implementation: https://github.com/mihakralj/pinescript/blob/main/indicators/channels/abber.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Abber : ITValuePublisher
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly RingBuffer _sourceBuffer;
    private readonly RingBuffer _deviationBuffer;
    private readonly TValuePublishedHandler _handler;

    private const int ResyncInterval = 1000;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumSource,
        double SumDeviation,
        double LastValidValue,
        int TickCount
    );
    private State _state;
    private State _pState;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of periods before the indicator is considered "hot" (valid).
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Current middle band value (SMA of source).
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current upper band value.
    /// </summary>
    public TValue Upper { get; private set; }

    /// <summary>
    /// Current lower band value.
    /// </summary>
    public TValue Lower { get; private set; }

    /// <summary>
    /// True if the indicator has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _sourceBuffer.IsFull;

    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates Abber with specified period and multiplier.
    /// </summary>
    /// <param name="period">Lookback period for SMA and deviation calculations (must be > 0)</param>
    /// <param name="multiplier">Multiplier for band width (must be > 0, default: 2.0)</param>
    public Abber(int period, double multiplier = 2.0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (multiplier <= 0)
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));

        _period = period;
        _multiplier = multiplier;
        _sourceBuffer = new RingBuffer(period);
        _deviationBuffer = new RingBuffer(period);
        Name = $"Abber({period},{multiplier:F2})";
        WarmupPeriod = period;
        _handler = HandleValue;
    }

    /// <summary>
    /// Creates Abber with TSeries source.
    /// </summary>
    public Abber(TSeries source, int period, double multiplier = 2.0) : this(period, multiplier)
    {
        Prime(source);
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates Abber with ITValuePublisher source.
    /// </summary>
    public Abber(ITValuePublisher source, int period, double multiplier = 2.0) : this(period, multiplier)
    {
        source.Pub += _handler;
    }

    private void HandleValue(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Helper to invoke the Pub event.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true)
    {
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });
    }

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double value, double deviation)
    {
        double removedSource = _sourceBuffer.Count == _sourceBuffer.Capacity ? _sourceBuffer.Oldest : 0.0;
        double removedDeviation = _deviationBuffer.Count == _deviationBuffer.Capacity ? _deviationBuffer.Oldest : 0.0;

        _state.SumSource = _state.SumSource - removedSource + value;
        _state.SumDeviation = _state.SumDeviation - removedDeviation + deviation;

        _sourceBuffer.Add(value);
        _deviationBuffer.Add(deviation);

        _state.TickCount++;
        if (_sourceBuffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            _state.SumSource = _sourceBuffer.RecalculateSum();
            _state.SumDeviation = _deviationBuffer.RecalculateSum();
        }
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double value = GetValidValue(input.Value);

        if (isNew)
        {
            _pState = _state;

            // Calculate SMA first to get deviation
            int count = _sourceBuffer.Count;
            double sma = count > 0 ? _state.SumSource / count : value;
            double deviation = Math.Abs(value - sma);

            UpdateState(value, deviation);
        }
        else
        {
            _state = _pState;

            // Calculate SMA first to get deviation
            int count = _sourceBuffer.Count;
            double sma = count > 0 ? _state.SumSource / count : value;
            double deviation = Math.Abs(value - sma);

            _sourceBuffer.UpdateNewest(value);
            _deviationBuffer.UpdateNewest(deviation);

            _state = _state with
            {
                SumSource = _sourceBuffer.Sum,
                SumDeviation = _deviationBuffer.Sum,
            };
        }

        int currentCount = _sourceBuffer.Count;
        if (currentCount == 0)
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = new TValue(input.Time, double.NaN);
            Lower = new TValue(input.Time, double.NaN);
        }
        else
        {
            double middle = _state.SumSource / currentCount;
            double avgDeviation = _state.SumDeviation / currentCount;
            double bandWidth = _multiplier * avgDeviation;

            Last = new TValue(input.Time, middle);
            Upper = new TValue(input.Time, middle + bandWidth);
            Lower = new TValue(input.Time, middle - bandWidth);
        }

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a TSeries.
    /// </summary>
    public (TSeries Middle, TSeries Upper, TSeries Lower) Update(TSeries source)
    {
        if (source.Count == 0)
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));

        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        var tSpan = CollectionsMarshal.AsSpan(tMiddle);
        var vMiddleSpan = CollectionsMarshal.AsSpan(vMiddle);
        var vUpperSpan = CollectionsMarshal.AsSpan(vUpper);
        var vLowerSpan = CollectionsMarshal.AsSpan(vLower);

        // Use batch calculation
        Batch(source.Values, vMiddleSpan, vUpperSpan, vLowerSpan, _period, _multiplier);

        source.Times.CopyTo(tSpan);

        // Copy timestamps to upper and lower (same time series)
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        // Prime the state for continued streaming
        Prime(source);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResyncSums(int period, ref WorkBuffers buffers, ref ScalarState state)
    {
        state.TickCount = 0;

        if (Vector.IsHardwareAccelerated && period >= Vector<double>.Count)
        {
            state.SumSource = SumSimd(buffers.Source);
            state.SumDeviation = SumSimd(buffers.Deviation);
        }
        else
        {
            double recalcSumSource = 0, recalcSumDeviation = 0;
            for (int k = 0; k < period; k++)
            {
                recalcSumSource += buffers.Source[k];
                recalcSumDeviation += buffers.Deviation[k];
            }
            state.SumSource = recalcSumSource;
            state.SumDeviation = recalcSumDeviation;
        }
    }

    private static double SumSimd(ReadOnlySpan<double> source)
    {
        var sumVector = Vector<double>.Zero;
        int i = 0;
        int size = Vector<double>.Count;
        int len = source.Length;

        for (; i <= len - size; i += size)
        {
            sumVector += new Vector<double>(source.Slice(i, size));
        }

        double sum = Vector.Sum(sumVector);

        for (; i < len; i++)
        {
            sum += source[i];
        }

        return sum;
    }

    /// <summary>
    /// Initializes the indicator state using the provided TSeries history.
    /// </summary>
    public void Prime(TSeries source)
    {
        if (source.Count == 0) return;

        // Reset state
        _sourceBuffer.Clear();
        _deviationBuffer.Clear();
        _state = default;
        _pState = default;

        int warmupLength = Math.Min(source.Count, WarmupPeriod);
        int startIndex = source.Count - warmupLength;

        // Seed LastValidValue
        _state.LastValidValue = double.NaN;

        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i].Value))
            {
                _state.LastValidValue = source[i].Value;
                break;
            }
        }

        // Find valid value in warmup window if not found
        if (double.IsNaN(_state.LastValidValue))
        {
            for (int i = startIndex; i < source.Count; i++)
            {
                if (double.IsFinite(source[i].Value))
                {
                    _state.LastValidValue = source[i].Value;
                    break;
                }
            }
        }

        // Feed the buffers
        for (int i = startIndex; i < source.Count; i++)
        {
            double value = GetValidValue(source[i].Value);

            // Calculate SMA to get deviation
            int count = _sourceBuffer.Count;
            double sma = count > 0 ? _state.SumSource / count : value;
            double deviation = Math.Abs(value - sma);

            UpdateState(value, deviation);
        }

        // Finalize state
        int currentCount = _sourceBuffer.Count;
        if (currentCount > 0)
        {
            var lastItem = source.Last;
            double middle = _state.SumSource / currentCount;
            double avgDeviation = _state.SumDeviation / currentCount;
            double bandWidth = _multiplier * avgDeviation;

            Last = new TValue(lastItem.Time, middle);
            Upper = new TValue(lastItem.Time, middle + bandWidth);
            Lower = new TValue(lastItem.Time, middle - bandWidth);
        }

        _pState = _state;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    public void Reset()
    {
        _sourceBuffer.Clear();
        _deviationBuffer.Clear();
        _state = default;
        _pState = default;
        Last = default;
        Upper = default;
        Lower = default;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Static Batch Methods
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Output buffers for batch Abber calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
#pragma warning disable S1104 // Fields should not have public accessibility
    public ref struct BatchOutputs
    {
        /// <summary>Output middle band (SMA of source)</summary>
        public Span<double> Middle;
        /// <summary>Output upper band</summary>
        public Span<double> Upper;
        /// <summary>Output lower band</summary>
        public Span<double> Lower;
#pragma warning restore S1104

        /// <summary>
        /// Creates a new BatchOutputs instance.
        /// </summary>
        public BatchOutputs(Span<double> middle, Span<double> upper, Span<double> lower)
        {
            Middle = middle;
            Upper = upper;
            Lower = lower;
        }
    }

    /// <summary>
    /// Internal state for scalar calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private ref struct ScalarState
    {
        public double SumSource;
        public double SumDeviation;
        public double LastValidValue;
        public int SourceBufferIndex;
        public int DeviationBufferIndex;
        public int TickCount;
    }

    /// <summary>
    /// Working buffers for batch calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private ref struct WorkBuffers
    {
        public Span<double> Source;
        public Span<double> Deviation;

        public WorkBuffers(Span<double> source, Span<double> deviation)
        {
            Source = source;
            Deviation = deviation;
        }
    }

    /// <summary>
    /// Calculates Abber for the entire TSeries using a new instance.
    /// </summary>
    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TSeries source, int period, double multiplier = 2.0)
    {
        var abber = new Abber(period, multiplier);
        return abber.Update(source);
    }

    /// <summary>
    /// Calculates Abber in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="source">Source price values</param>
    /// <param name="outputs">Output buffers for middle, upper, and lower bands</param>
    /// <param name="period">Lookback period</param>
    /// <param name="multiplier">Band width multiplier</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> source,
        BatchOutputs outputs,
        int period,
        double multiplier = 2.0)
    {
        Batch(source, outputs.Middle, outputs.Upper, outputs.Lower, period, multiplier);
    }

    /// <summary>
    /// Calculates Abber in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="source">Source price values</param>
    /// <param name="middle">Output middle band (SMA of source)</param>
    /// <param name="upper">Output upper band</param>
    /// <param name="lower">Output lower band</param>
    /// <param name="period">Lookback period</param>
    /// <param name="multiplier">Band width multiplier</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double multiplier = 2.0)
    {
        int len = source.Length;
        if (middle.Length < len || upper.Length < len || lower.Length < len)
            throw new ArgumentException("Output buffers must be at least as long as input", nameof(middle));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (multiplier <= 0)
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));

        if (len == 0) return;

        // Scalar implementation with NaN handling
        var outputs = new BatchOutputs(middle, upper, lower);
        CalculateScalarCore(source, outputs, period, multiplier);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(
        ReadOnlySpan<double> source,
        scoped BatchOutputs outputs,
        int period,
        double multiplier)
    {
        int len = source.Length;

        // Always use ArrayPool to avoid span scope safety issues with stackalloc + ref structs
        double[] rentedSource = ArrayPool<double>.Shared.Rent(period);
        double[] rentedDeviation = ArrayPool<double>.Shared.Rent(period);

        try
        {
            var buffers = new WorkBuffers(
                rentedSource.AsSpan(0, period),
                rentedDeviation.AsSpan(0, period));

            var state = new ScalarState
            {
                LastValidValue = double.NaN,
            };

            SeedFirstValidValue(source, ref state);

            int warmupEnd = Math.Min(period, len);
            ProcessWarmupPhase(source, outputs, warmupEnd, multiplier, ref buffers, ref state);
            ProcessMainLoop(source, outputs, warmupEnd, period, multiplier, ref buffers, ref state);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedSource);
            ArrayPool<double>.Shared.Return(rentedDeviation);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SeedFirstValidValue(ReadOnlySpan<double> source, ref ScalarState state)
    {
        int len = source.Length;
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                state.LastValidValue = source[k];
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(ReadOnlySpan<double> source, int i, ref ScalarState state)
    {
        double v = source[i];
        if (double.IsFinite(v))
        {
            state.LastValidValue = v;
            return v;
        }
        return state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBandOutputs(scoped BatchOutputs outputs, int i, double middle, double avgDeviation, double multiplier)
    {
        double bandWidth = multiplier * avgDeviation;
        outputs.Middle[i] = middle;
        outputs.Upper[i] = middle + bandWidth;
        outputs.Lower[i] = middle - bandWidth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessWarmupPhase(
        ReadOnlySpan<double> source,
        scoped BatchOutputs outputs,
        int warmupEnd,
        double multiplier,
        ref WorkBuffers buffers,
        ref ScalarState state)
    {
        for (int i = 0; i < warmupEnd; i++)
        {
            double v = GetValidValue(source, i, ref state);

            // Calculate current SMA to get deviation
            int count = i;
            double sma = count > 0 ? state.SumSource / count : v;
            double deviation = Math.Abs(v - sma);

            state.SumSource += v;
            state.SumDeviation += deviation;

            buffers.Source[i] = v;
            buffers.Deviation[i] = deviation;

            int newCount = i + 1;
            double middle = state.SumSource / newCount;
            double avgDeviation = state.SumDeviation / newCount;
            WriteBandOutputs(outputs, i, middle, avgDeviation, multiplier);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessMainLoop(
        ReadOnlySpan<double> source,
        scoped BatchOutputs outputs,
        int startIndex,
        int period,
        double multiplier,
        ref WorkBuffers buffers,
        ref ScalarState state)
    {
        int len = source.Length;
        for (int i = startIndex; i < len; i++)
        {
            double v = GetValidValue(source, i, ref state);

            // Calculate current SMA to get deviation
            double sma = state.SumSource / period;
            double deviation = Math.Abs(v - sma);

            // Update source running sum
            state.SumSource = state.SumSource - buffers.Source[state.SourceBufferIndex] + v;
            buffers.Source[state.SourceBufferIndex] = v;

            // Update deviation running sum
            state.SumDeviation = state.SumDeviation - buffers.Deviation[state.DeviationBufferIndex] + deviation;
            buffers.Deviation[state.DeviationBufferIndex] = deviation;

            state.SourceBufferIndex++;
            if (state.SourceBufferIndex >= period) state.SourceBufferIndex = 0;
            state.DeviationBufferIndex++;
            if (state.DeviationBufferIndex >= period) state.DeviationBufferIndex = 0;

            double middle = state.SumSource / period;
            double avgDeviation = state.SumDeviation / period;
            WriteBandOutputs(outputs, i, middle, avgDeviation, multiplier);

            state.TickCount++;
            if (state.TickCount >= ResyncInterval)
            {
                ResyncSums(period, ref buffers, ref state);
            }
        }
    }

    /// <summary>
    /// Runs a high-performance batch calculation and returns a "Hot" Abber instance.
    /// </summary>
    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Abber Indicator) Calculate(TSeries source, int period, double multiplier = 2.0)
    {
        var abber = new Abber(period, multiplier);
        var results = abber.Update(source);
        return (results, abber);
    }
}
