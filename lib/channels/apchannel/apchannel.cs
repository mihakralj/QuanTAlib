using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// APChannel: Average Price Channel
/// </summary>
/// <remarks>
/// The Average Price Channel is a simple volatility channel indicator.
/// It uses the Simple Moving Average (SMA) of High, Low, and the average price
/// to define its upper, lower, and middle bands respectively.
///
/// Calculation:
/// Middle Band = SMA((High + Low + Close) / 3, Period)
/// Upper Band = SMA(High, Period)
/// Lower Band = SMA(Low, Period)
///
/// Key characteristics:
/// - Provides a smoothed channel based on moving averages of key price points.
/// - Simpler calculation compared to other volatility channels.
///
/// Sources:
/// General concept of moving average channels.
/// </remarks>
[SkipLocalsInit]
public sealed class APChannel : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _highBuffer;
    private readonly RingBuffer _lowBuffer;
    private readonly RingBuffer _closeBuffer;
    private readonly RingBuffer _avgPriceBuffer; // For Middle Band SMA
    private readonly TBarPublishedHandler _barHandler;

    private const int ResyncInterval = 1000;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumHigh,
        double SumLow,
        double SumClose, // Used for average price calculation
        double SumAvgPrice, // For Middle Band
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        int TickCount
    );
    private State _state;
    private State _p_state;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of periods before the indicator is considered "hot" (valid).
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Current middle band value.
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
    public bool IsHot => _avgPriceBuffer.IsFull;

    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates APChannel with specified period.
    /// </summary>
    /// <param name="period">Lookback period for SMA calculations (must be > 0)</param>
    public APChannel(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _highBuffer = new RingBuffer(period);
        _lowBuffer = new RingBuffer(period);
        _closeBuffer = new RingBuffer(period);
        _avgPriceBuffer = new RingBuffer(period);
        Name = $"APChannel({period})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates APChannel with TBarSeries source.
    /// </summary>
    public APChannel(TBarSeries source, int period) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

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
    private double GetValidHigh(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidHigh = input;
            return input;
        }
        return _state.LastValidHigh;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidLow(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidLow = input;
            return input;
        }
        return _state.LastValidLow;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidClose(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidClose = input;
            return input;
        }
        return _state.LastValidClose;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double high, double low, double close)
    {
        double removedHigh = _highBuffer.Count == _highBuffer.Capacity ? _highBuffer.Oldest : 0.0;
        double removedLow = _lowBuffer.Count == _lowBuffer.Capacity ? _lowBuffer.Oldest : 0.0;
        double removedClose = _closeBuffer.Count == _closeBuffer.Capacity ? _closeBuffer.Oldest : 0.0;
        double removedAvgPrice = _avgPriceBuffer.Count == _avgPriceBuffer.Capacity ? _avgPriceBuffer.Oldest : 0.0;

        double avgPrice = (high + low + close) / 3.0;

        _state.SumHigh = _state.SumHigh - removedHigh + high;
        _state.SumLow = _state.SumLow - removedLow + low;
        _state.SumClose = _state.SumClose - removedClose + close;
        _state.SumAvgPrice = _state.SumAvgPrice - removedAvgPrice + avgPrice;


        _highBuffer.Add(high);
        _lowBuffer.Add(low);
        _closeBuffer.Add(close);
        _avgPriceBuffer.Add(avgPrice);

        _state.TickCount++;
        if (_avgPriceBuffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            _state.SumHigh = _highBuffer.RecalculateSum();
            _state.SumLow = _lowBuffer.RecalculateSum();
            _state.SumClose = _closeBuffer.RecalculateSum();
            _state.SumAvgPrice = _avgPriceBuffer.RecalculateSum();
        }
    }

    /// <summary>
    /// Updates the indicator with a TBar input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;

            double high = GetValidHigh(input.High);
            double low = GetValidLow(input.Low);
            double close = GetValidClose(input.Close);
            UpdateState(high, low, close);
        }
        else
        {
            _state = _p_state;

            double high = GetValidHigh(input.High);
            double low = GetValidLow(input.Low);
            double close = GetValidClose(input.Close);
            double avgPrice = (high + low + close) / 3.0;


            _highBuffer.UpdateNewest(high);
            _lowBuffer.UpdateNewest(low);
            _closeBuffer.UpdateNewest(close);
            _avgPriceBuffer.UpdateNewest(avgPrice);


            _state = _state with
            {
                SumHigh = _highBuffer.Sum,
                SumLow = _lowBuffer.Sum,
                SumClose = _closeBuffer.Sum,
                SumAvgPrice = _avgPriceBuffer.Sum
            };
        }

        int count = _avgPriceBuffer.Count;
        if (count == 0)
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = new TValue(input.Time, double.NaN);
            Lower = new TValue(input.Time, double.NaN);
        }
        else
        {
            double smaHigh = _state.SumHigh / count;
            double smaLow = _state.SumLow / count;
            double smaAvgPrice = _state.SumAvgPrice / count;

            Last = new TValue(input.Time, smaAvgPrice);
            Upper = new TValue(input.Time, smaHigh);
            Lower = new TValue(input.Time, smaLow);
        }

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a TBarSeries.
    /// </summary>
    public (TSeries Middle, TSeries Upper, TSeries Lower) Update(TBarSeries source)
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

        // Pre-calculate Average Price values for batch
        double[] avgPrices = ArrayPool<double>.Shared.Rent(len);
        try
        {
            for (int i = 0; i < len; i++)
            {
                avgPrices[i] = (source.HighValues[i] + source.LowValues[i] + source.CloseValues[i]) / 3.0;
            }

            // Use batch calculation
            Batch(source.HighValues, source.LowValues, source.CloseValues, avgPrices.AsSpan(0, len),
                  vMiddleSpan, vUpperSpan, vLowerSpan, _period);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(avgPrices);
        }
        
        source.Times.CopyTo(tSpan);

        // Copy timestamps to upper and lower (same time series)
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        // Prime the state for continued streaming
        Prime(source);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    /// <summary>
    /// Initializes the indicator state using the provided TBarSeries history.
    /// </summary>
    // skipcq: CS-R1140
    public void Prime(TBarSeries source)
    {
        if (source.Count == 0) return;

        // Reset state
        _highBuffer.Clear();
        _lowBuffer.Clear();
        _closeBuffer.Clear();
        _avgPriceBuffer.Clear();
        _state = default;
        _p_state = default;

        int warmupLength = Math.Min(source.Count, WarmupPeriod);
        int startIndex = source.Count - warmupLength;

        // Seed LastValidValue
        _state.LastValidHigh = double.NaN;
        _state.LastValidLow = double.NaN;
        _state.LastValidClose = double.NaN;

        for (int i = startIndex - 1; i >= 0; i--)
        {
            var bar = source[i];
            if (double.IsFinite(bar.High) && double.IsNaN(_state.LastValidHigh))
                _state.LastValidHigh = bar.High;
            if (double.IsFinite(bar.Low) && double.IsNaN(_state.LastValidLow))
                _state.LastValidLow = bar.Low;
            if (double.IsFinite(bar.Close) && double.IsNaN(_state.LastValidClose))
                _state.LastValidClose = bar.Close;
            if (!double.IsNaN(_state.LastValidHigh) && !double.IsNaN(_state.LastValidLow) && !double.IsNaN(_state.LastValidClose))
                break;
        }

        // Find valid values in warmup window if not found
        if (double.IsNaN(_state.LastValidHigh) || double.IsNaN(_state.LastValidLow) || double.IsNaN(_state.LastValidClose))
        {
            for (int i = startIndex; i < source.Count; i++)
            {
                var bar = source[i];
                if (double.IsFinite(bar.High) && double.IsNaN(_state.LastValidHigh))
                    _state.LastValidHigh = bar.High;
                if (double.IsFinite(bar.Low) && double.IsNaN(_state.LastValidLow))
                    _state.LastValidLow = bar.Low;
                if (double.IsFinite(bar.Close) && double.IsNaN(_state.LastValidClose))
                    _state.LastValidClose = bar.Close;
                if (!double.IsNaN(_state.LastValidHigh) && !double.IsNaN(_state.LastValidLow) && !double.IsNaN(_state.LastValidClose))
                    break;
            }
        }

        // Feed the buffers
        for (int i = startIndex; i < source.Count; i++)
        {
            var bar = source[i];
            double high = GetValidHigh(bar.High);
            double low = GetValidLow(bar.Low);
            double close = GetValidClose(bar.Close);
            UpdateState(high, low, close);
        }

        // Finalize state
        int count = _avgPriceBuffer.Count;
        if (count > 0)
        {
            var lastBar = source.Last;
            double smaHigh = _state.SumHigh / count;
            double smaLow = _state.SumLow / count;
            double smaAvgPrice = _state.SumAvgPrice / count;

            Last = new TValue(lastBar.Time, smaAvgPrice);
            Upper = new TValue(lastBar.Time, smaHigh);
            Lower = new TValue(lastBar.Time, smaLow);
        }

        _p_state = _state;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    public void Reset()
    {
        _highBuffer.Clear();
        _lowBuffer.Clear();
        _closeBuffer.Clear();
        _avgPriceBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
        Upper = default;
        Lower = default;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Static Batch Methods
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Output buffers for batch APChannel calculation.
    /// </summary>
    /// <remarks>
    /// Public Span fields are intentional: ref structs cannot use auto-properties with Span&lt;T&gt;
    /// and direct field access provides optimal performance for this high-throughput API.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
#pragma warning disable S1104 // Fields should not have public accessibility
    public ref struct BatchOutputs
    {
        /// <summary>Output middle band (SMA of average price)</summary>
        public Span<double> Middle;
        /// <summary>Output upper band (SMA of high)</summary>
        public Span<double> Upper;
        /// <summary>Output lower band (SMA of low)</summary>
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
    /// Input buffers for batch APChannel calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
#pragma warning disable S1104 // Fields should not have public accessibility
    public ref struct BatchInputs
    {
        /// <summary>High price values</summary>
        public ReadOnlySpan<double> High;
        /// <summary>Low price values</summary>
        public ReadOnlySpan<double> Low;
        /// <summary>Close price values</summary>
        public ReadOnlySpan<double> Close;
        /// <summary>Average price values (High+Low+Close)/3</summary>
        public ReadOnlySpan<double> AvgPrice;
#pragma warning restore S1104

        /// <summary>
        /// Creates a new BatchInputs instance.
        /// </summary>
        public BatchInputs(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> avgPrice)
        {
            High = high;
            Low = low;
            Close = close;
            AvgPrice = avgPrice;
        }
    }

    /// <summary>
    /// Internal state for scalar calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private ref struct ScalarState
    {
        public double SumHigh;
        public double SumLow;
        public double SumAvgPrice;
        public double LastValidHigh;
        public double LastValidLow;
        public double LastValidClose;
        public int BufferIndex;
        public int TickCount;
    }

    /// <summary>
    /// Working buffers for batch calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private ref struct WorkBuffers
    {
        public Span<double> High;
        public Span<double> Low;
        public Span<double> AvgPrice;

        public WorkBuffers(Span<double> high, Span<double> low, Span<double> avgPrice)
        {
            High = high;
            Low = low;
            AvgPrice = avgPrice;
        }
    }

    /// <summary>
    /// Calculates APChannel for the entire TBarSeries using a new instance.
    /// </summary>
    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period)
    {
        var apChannel = new APChannel(period);
        return apChannel.Update(source);
    }

    /// <summary>
    /// Calculates APChannel in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="inputs">Input buffers for high, low, close, and average prices</param>
    /// <param name="outputs">Output buffers for middle, upper, and lower bands</param>
    /// <param name="period">Lookback period</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        BatchInputs inputs,
        BatchOutputs outputs,
        int period)
    {
        Batch(inputs.High, inputs.Low, inputs.Close, inputs.AvgPrice, outputs.Middle, outputs.Upper, outputs.Lower, period);
    }

    /// <summary>
    /// Calculates APChannel in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="high">High price values</param>
    /// <param name="low">Low price values</param>
    /// <param name="close">Close price values</param>
    /// <param name="avgPrice">Average price values</param>
    /// <param name="outputs">Output buffers for middle, upper, and lower bands</param>
    /// <param name="period">Lookback period</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        ReadOnlySpan<double> avgPrice,
        BatchOutputs outputs,
        int period)
    {
        Batch(high, low, close, avgPrice, outputs.Middle, outputs.Upper, outputs.Lower, period);
    }

    /// <summary>
    /// Calculates APChannel in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="high">High price values</param>
    /// <param name="low">Low price values</param>
    /// <param name="close">Close price values</param>
    /// <param name="avgPrice">Average price values</param>
    /// <param name="middle">Output middle band (SMA of average price)</param>
    /// <param name="upper">Output upper band (SMA of high)</param>
    /// <param name="lower">Output lower band (SMA of low)</param>
    /// <param name="period">Lookback period</param>
    // Suppressing S107: This is a high-performance batch API where callers benefit from
    // direct span parameters. A BatchOutputs overload exists for callers preferring fewer parameters.
#pragma warning disable S107
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, // Close is needed for GetValidHLC but not for direct SMA
        ReadOnlySpan<double> avgPrice,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period)
#pragma warning restore S107
    {
        int len = close.Length; // Use close length as reference
        if (high.Length != len || low.Length != len || avgPrice.Length != len)
            throw new ArgumentException("Input spans must have the same length", nameof(high));
        if (middle.Length < len || upper.Length < len || lower.Length < len)
            throw new ArgumentException("Output buffers must be at least as long as input", nameof(middle));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        if (len == 0) return;

        // Scalar implementation with NaN handling
        var inputs = new BatchInputs(high, low, close, avgPrice);
        var outputs = new BatchOutputs(middle, upper, lower);
        CalculateScalarCore(inputs, outputs, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(
        scoped BatchInputs inputs,
        scoped BatchOutputs outputs,
        int period)
    {
        int len = inputs.Close.Length;

        // Always use ArrayPool to avoid span scope safety issues with stackalloc + ref structs
        double[] rentedHigh = ArrayPool<double>.Shared.Rent(period);
        double[] rentedLow = ArrayPool<double>.Shared.Rent(period);
        double[] rentedAvgPrice = ArrayPool<double>.Shared.Rent(period);

        try
        {
            var buffers = new WorkBuffers(
                rentedHigh.AsSpan(0, period),
                rentedLow.AsSpan(0, period),
                rentedAvgPrice.AsSpan(0, period));

            var state = new ScalarState
            {
                LastValidHigh = double.NaN,
                LastValidLow = double.NaN,
                LastValidClose = double.NaN // Still needed for GetValidHLC
            };

            SeedFirstValidValues(inputs, ref state);

            int warmupEnd = Math.Min(period, len);
            ProcessWarmupPhase(inputs, outputs, warmupEnd, ref buffers, ref state);
            ProcessMainLoop(inputs, outputs, warmupEnd, period, ref buffers, ref state);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedHigh);
            ArrayPool<double>.Shared.Return(rentedLow);
            ArrayPool<double>.Shared.Return(rentedAvgPrice);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SeedFirstValidValues(scoped BatchInputs inputs, ref ScalarState state)
    {
        int len = inputs.Close.Length;
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(inputs.High[k]) && double.IsNaN(state.LastValidHigh))
                state.LastValidHigh = inputs.High[k];
            if (double.IsFinite(inputs.Low[k]) && double.IsNaN(state.LastValidLow))
                state.LastValidLow = inputs.Low[k];
            if (double.IsFinite(inputs.Close[k]) && double.IsNaN(state.LastValidClose))
                state.LastValidClose = inputs.Close[k];
            if (!double.IsNaN(state.LastValidHigh) && !double.IsNaN(state.LastValidLow) && !double.IsNaN(state.LastValidClose))
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double h, double l, double c) GetValidHLC(scoped BatchInputs inputs, int i, ref ScalarState state)
    {
        double h = inputs.High[i];
        double l = inputs.Low[i];
        double c = inputs.Close[i];

        if (double.IsFinite(h)) state.LastValidHigh = h; else h = state.LastValidHigh;
        if (double.IsFinite(l)) state.LastValidLow = l; else l = state.LastValidLow;
        if (double.IsFinite(c)) state.LastValidClose = c; else c = state.LastValidClose;

        return (h, l, c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBandOutputs(scoped BatchOutputs outputs, int i, double smaHigh, double smaLow, double smaAvgPrice)
    {
        outputs.Middle[i] = smaAvgPrice;
        outputs.Upper[i] = smaHigh;
        outputs.Lower[i] = smaLow;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessWarmupPhase(
        scoped BatchInputs inputs,
        scoped BatchOutputs outputs,
        int warmupEnd,
        ref WorkBuffers buffers,
        ref ScalarState state)
    {
        for (int i = 0; i < warmupEnd; i++)
        {
            var (h, l, _) = GetValidHLC(inputs, i, ref state); // Discard 'c' as it's not used here
            double avgPrice = inputs.AvgPrice[i];

            state.SumHigh += h;
            state.SumLow += l;
            state.SumAvgPrice += avgPrice;

            buffers.High[i] = h;
            buffers.Low[i] = l;
            buffers.AvgPrice[i] = avgPrice;

            int count = i + 1;
            WriteBandOutputs(outputs, i, state.SumHigh / count, state.SumLow / count, state.SumAvgPrice / count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessMainLoop(
        scoped BatchInputs inputs,
        scoped BatchOutputs outputs,
        int startIndex,
        int period,
        ref WorkBuffers buffers,
        ref ScalarState state)
    {
        int len = inputs.Close.Length;
        for (int i = startIndex; i < len; i++)
        {
            var (h, l, _) = GetValidHLC(inputs, i, ref state); // Discard 'c' as it's not used here
            double avgPrice = inputs.AvgPrice[i];

            state.SumHigh = state.SumHigh - buffers.High[state.BufferIndex] + h;
            state.SumLow = state.SumLow - buffers.Low[state.BufferIndex] + l;
            state.SumAvgPrice = state.SumAvgPrice - buffers.AvgPrice[state.BufferIndex] + avgPrice;

            buffers.High[state.BufferIndex] = h;
            buffers.Low[state.BufferIndex] = l;
            buffers.AvgPrice[state.BufferIndex] = avgPrice;

            state.BufferIndex++;
            if (state.BufferIndex >= period) state.BufferIndex = 0;

            WriteBandOutputs(outputs, i, state.SumHigh / period, state.SumLow / period, state.SumAvgPrice / period);

            state.TickCount++;
            if (state.TickCount >= ResyncInterval)
            {
                ResyncSums(period, ref buffers, ref state);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResyncSums(int period, ref WorkBuffers buffers, ref ScalarState state)
    {
        state.TickCount = 0;
        if (Vector.IsHardwareAccelerated && period >= Vector<double>.Count)
        {
            state.SumHigh = SumSimd(buffers.High);
            state.SumLow = SumSimd(buffers.Low);
            state.SumAvgPrice = SumSimd(buffers.AvgPrice);
        }
        else
        {
            double recalcSumHigh = 0, recalcSumLow = 0, recalcSumAvgPrice = 0;
            for (int k = 0; k < period; k++)
            {
                recalcSumHigh += buffers.High[k];
                recalcSumLow += buffers.Low[k];
                recalcSumAvgPrice += buffers.AvgPrice[k];
            }
            state.SumHigh = recalcSumHigh;
            state.SumLow = recalcSumLow;
            state.SumAvgPrice = recalcSumAvgPrice;
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
    /// Runs a high-performance batch calculation and returns a "Hot" APChannel instance.
    /// </summary>
    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, APChannel Indicator) Calculate(TBarSeries source, int period)
    {
        var apChannel = new APChannel(period);
        var results = apChannel.Update(source);
        return (results, apChannel);
    }
}
