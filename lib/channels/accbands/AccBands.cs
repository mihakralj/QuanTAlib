using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AccBands: Acceleration Bands
/// </summary>
/// <remarks>
/// Acceleration Bands are a volatility-based channel indicator developed by Price Headley.
/// They create an adaptive price envelope around a moving average, where the band width
/// is determined by the per-bar normalized range applied before averaging.
///
/// Calculation (Headley's original formula):
/// w = (High - Low) / (High + Low)          // normalized range width per bar
/// Upper Band = SMA(High × (1 + factor × w), Period)
/// Lower Band = SMA(Low × (1 - factor × w), Period)
/// Middle Band = SMA(Close, Period)
///
/// Key characteristics:
/// - Width adjustment is applied per bar before averaging (Headley's method)
/// - Bands expand during volatile periods and contract during consolidation
/// - Factor parameter (default 4.0) controls band sensitivity
///
/// Sources:
/// Headley, P. (2002). Big Trends in Trading. John Wiley &amp; Sons.
/// </remarks>
[SkipLocalsInit]
public sealed class AccBands : ITValuePublisher, IDisposable
{
    private readonly int _period;
    private readonly double _factor;
    private readonly RingBuffer _adjHighBuffer;
    private readonly RingBuffer _adjLowBuffer;
    private readonly RingBuffer _closeBuffer;
    private readonly TBarPublishedHandler _barHandler;
    private TBarSeries? _source;
    private bool _disposed;

    private const int ResyncInterval = 1000;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumAdjHigh,
        double SumAdjLow,
        double SumClose,
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
    public bool IsHot => _closeBuffer.IsFull;

    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates AccBands with specified period and factor.
    /// </summary>
    /// <param name="period">Lookback period for SMA calculations (must be > 0)</param>
    /// <param name="factor">Multiplier for normalized width (must be > 0, default: 4.0 per Headley)</param>
    public AccBands(int period, double factor = 4.0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (factor <= 0)
        {
            throw new ArgumentException("Factor must be greater than 0", nameof(factor));
        }

        _period = period;
        _factor = factor;
        _adjHighBuffer = new RingBuffer(period);
        _adjLowBuffer = new RingBuffer(period);
        _closeBuffer = new RingBuffer(period);
        Name = $"AccBands({period},{factor:F2})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates AccBands with TBarSeries source.
    /// </summary>
    public AccBands(TBarSeries source, int period, double factor = 4.0) : this(period, factor)
    {
        _source = source;
        Prime(source);
        source.Pub += _barHandler;
    }

    /// <summary>
    /// Releases resources and unsubscribes from the source event.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_source != null)
        {
            _source.Pub -= _barHandler;
            _source = null;
        }
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

    /// <summary>
    /// Computes Headley's per-bar adjusted values and updates running sums.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double high, double low, double close)
    {
        // Headley's per-bar normalized width
        double denom = high + low;
        double w = denom != 0.0 ? (high - low) / denom : 0.0;
        double adjHigh = high * (1.0 + _factor * w);
        double adjLow = low * (1.0 - _factor * w);

        double removedAdjHigh = _adjHighBuffer.Count == _adjHighBuffer.Capacity ? _adjHighBuffer.Oldest : 0.0;
        double removedAdjLow = _adjLowBuffer.Count == _adjLowBuffer.Capacity ? _adjLowBuffer.Oldest : 0.0;
        double removedClose = _closeBuffer.Count == _closeBuffer.Capacity ? _closeBuffer.Oldest : 0.0;

        _state.SumAdjHigh = _state.SumAdjHigh - removedAdjHigh + adjHigh;
        _state.SumAdjLow = _state.SumAdjLow - removedAdjLow + adjLow;
        _state.SumClose = _state.SumClose - removedClose + close;

        _adjHighBuffer.Add(adjHigh);
        _adjLowBuffer.Add(adjLow);
        _closeBuffer.Add(close);

        _state.TickCount++;
        if (_closeBuffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            _state.SumAdjHigh = _adjHighBuffer.RecalculateSum();
            _state.SumAdjLow = _adjLowBuffer.RecalculateSum();
            _state.SumClose = _closeBuffer.RecalculateSum();
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

            // Recompute adjusted values for the corrected bar
            double denom = high + low;
            double w = denom != 0.0 ? (high - low) / denom : 0.0;
            double adjHigh = high * (1.0 + _factor * w);
            double adjLow = low * (1.0 - _factor * w);

            _adjHighBuffer.UpdateNewest(adjHigh);
            _adjLowBuffer.UpdateNewest(adjLow);
            _closeBuffer.UpdateNewest(close);

            _state = _state with
            {
                SumAdjHigh = _adjHighBuffer.Sum,
                SumAdjLow = _adjLowBuffer.Sum,
                SumClose = _closeBuffer.Sum,
            };
        }

        int count = _closeBuffer.Count;
        if (count == 0)
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = new TValue(input.Time, double.NaN);
            Lower = new TValue(input.Time, double.NaN);
        }
        else
        {
            double smaAdjHigh = _state.SumAdjHigh / count;
            double smaAdjLow = _state.SumAdjLow / count;
            double smaClose = _state.SumClose / count;

            Last = new TValue(input.Time, smaClose);
            Upper = new TValue(input.Time, smaAdjHigh);
            Lower = new TValue(input.Time, smaAdjLow);
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
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));
        }

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
        Batch(source.HighValues, source.LowValues, source.CloseValues,
              vMiddleSpan, vUpperSpan, vLowerSpan, _period, _factor);

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
        if (source.Count == 0)
        {
            return;
        }

        // Reset state
        _adjHighBuffer.Clear();
        _adjLowBuffer.Clear();
        _closeBuffer.Clear();
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
            {
                _state.LastValidHigh = bar.High;
            }

            if (double.IsFinite(bar.Low) && double.IsNaN(_state.LastValidLow))
            {
                _state.LastValidLow = bar.Low;
            }

            if (double.IsFinite(bar.Close) && double.IsNaN(_state.LastValidClose))
            {
                _state.LastValidClose = bar.Close;
            }

            if (!double.IsNaN(_state.LastValidHigh) && !double.IsNaN(_state.LastValidLow) && !double.IsNaN(_state.LastValidClose))
            {
                break;
            }
        }

        // Find valid values in warmup window if not found
        if (double.IsNaN(_state.LastValidHigh) || double.IsNaN(_state.LastValidLow) || double.IsNaN(_state.LastValidClose))
        {
            for (int i = startIndex; i < source.Count; i++)
            {
                var bar = source[i];
                if (double.IsFinite(bar.High) && double.IsNaN(_state.LastValidHigh))
                {
                    _state.LastValidHigh = bar.High;
                }

                if (double.IsFinite(bar.Low) && double.IsNaN(_state.LastValidLow))
                {
                    _state.LastValidLow = bar.Low;
                }

                if (double.IsFinite(bar.Close) && double.IsNaN(_state.LastValidClose))
                {
                    _state.LastValidClose = bar.Close;
                }

                if (!double.IsNaN(_state.LastValidHigh) && !double.IsNaN(_state.LastValidLow) && !double.IsNaN(_state.LastValidClose))
                {
                    break;
                }
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
        int count = _closeBuffer.Count;
        if (count > 0)
        {
            var lastBar = source.Last;
            double smaAdjHigh = _state.SumAdjHigh / count;
            double smaAdjLow = _state.SumAdjLow / count;
            double smaClose = _state.SumClose / count;

            Last = new TValue(lastBar.Time, smaClose);
            Upper = new TValue(lastBar.Time, smaAdjHigh);
            Lower = new TValue(lastBar.Time, smaAdjLow);
        }

        _p_state = _state;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    public void Reset()
    {
        _adjHighBuffer.Clear();
        _adjLowBuffer.Clear();
        _closeBuffer.Clear();
        _state = new State(
            SumAdjHigh: 0,
            SumAdjLow: 0,
            SumClose: 0,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN,
            TickCount: 0
        );
        _p_state = _state;
        Last = default;
        Upper = default;
        Lower = default;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Static Batch Methods
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Output buffers for batch AccBands calculation.
    /// </summary>
    /// <remarks>
    /// Public Span fields are intentional: ref structs cannot use auto-properties with Span&lt;T&gt;
    /// and direct field access provides optimal performance for this high-throughput API.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
#pragma warning disable S1104 // Fields should not have public accessibility
    public ref struct BatchOutputs
    {
        /// <summary>Output middle band (SMA of close)</summary>
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
    /// Input buffers for batch AccBands calculation.
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
#pragma warning restore S1104

        /// <summary>
        /// Creates a new BatchInputs instance.
        /// </summary>
        public BatchInputs(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close)
        {
            High = high;
            Low = low;
            Close = close;
        }
    }

    /// <summary>
    /// Internal state for scalar calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private ref struct ScalarState
    {
        internal double SumAdjHigh;
        internal double SumAdjLow;
        internal double SumClose;
        internal double LastValidHigh;
        internal double LastValidLow;
        internal double LastValidClose;
        internal int BufferIndex;
        internal int TickCount;
    }

    /// <summary>
    /// Working buffers for batch calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct WorkBuffers(Span<double> adjHigh, Span<double> adjLow, Span<double> close)
    {
        internal readonly Span<double> AdjHigh = adjHigh;
        internal readonly Span<double> AdjLow = adjLow;
        internal readonly Span<double> Close = close;
    }

    /// <summary>
    /// Calculates AccBands for the entire TBarSeries using a new instance.
    /// </summary>
    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period, double factor = 4.0)
    {
        var accBands = new AccBands(period, factor);
        return accBands.Update(source);
    }

    /// <summary>
    /// Calculates AccBands in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="inputs">Input buffers for high, low, and close prices</param>
    /// <param name="outputs">Output buffers for middle, upper, and lower bands</param>
    /// <param name="period">Lookback period</param>
    /// <param name="factor">Band width factor</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        BatchInputs inputs,
        BatchOutputs outputs,
        int period,
        double factor = 4.0)
    {
        Batch(inputs.High, inputs.Low, inputs.Close, outputs.Middle, outputs.Upper, outputs.Lower, period, factor);
    }

    /// <summary>
    /// Calculates AccBands in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="high">High price values</param>
    /// <param name="low">Low price values</param>
    /// <param name="close">Close price values</param>
    /// <param name="outputs">Output buffers for middle, upper, and lower bands</param>
    /// <param name="period">Lookback period</param>
    /// <param name="factor">Band width factor</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        BatchOutputs outputs,
        int period,
        double factor = 4.0)
    {
        Batch(high, low, close, outputs.Middle, outputs.Upper, outputs.Lower, period, factor);
    }

    /// <summary>
    /// Calculates AccBands in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    /// <param name="high">High price values</param>
    /// <param name="low">Low price values</param>
    /// <param name="close">Close price values</param>
    /// <param name="middle">Output middle band (SMA of close)</param>
    /// <param name="upper">Output upper band</param>
    /// <param name="lower">Output lower band</param>
    /// <param name="period">Lookback period</param>
    /// <param name="factor">Band width factor</param>
    // Suppressing S107: This is a high-performance batch API where callers benefit from
    // direct span parameters. A BatchOutputs overload exists for callers preferring fewer parameters.
#pragma warning disable S107
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double factor = 4.0)
#pragma warning restore S107
    {
        int len = close.Length;
        if (high.Length != len || low.Length != len)
        {
            throw new ArgumentException("High, Low, and Close must have the same length", nameof(high));
        }

        if (middle.Length < len || upper.Length < len || lower.Length < len)
        {
            throw new ArgumentException("Output buffers must be at least as long as input", nameof(middle));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (factor <= 0)
        {
            throw new ArgumentException("Factor must be greater than 0", nameof(factor));
        }

        if (len == 0)
        {
            return;
        }

        // Scalar implementation with NaN handling
        var inputs = new BatchInputs(high, low, close);
        var outputs = new BatchOutputs(middle, upper, lower);
        CalculateScalarCore(inputs, outputs, period, factor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(
        scoped BatchInputs inputs,
        scoped BatchOutputs outputs,
        int period,
        double factor)
    {
        int len = inputs.Close.Length;

        // Always use ArrayPool to avoid span scope safety issues with stackalloc + ref structs
        double[] rentedAdjHigh = ArrayPool<double>.Shared.Rent(period);
        double[] rentedAdjLow = ArrayPool<double>.Shared.Rent(period);
        double[] rentedClose = ArrayPool<double>.Shared.Rent(period);

        try
        {
            var buffers = new WorkBuffers(
                rentedAdjHigh.AsSpan(0, period),
                rentedAdjLow.AsSpan(0, period),
                rentedClose.AsSpan(0, period));

            var state = new ScalarState
            {
                LastValidHigh = double.NaN,
                LastValidLow = double.NaN,
                LastValidClose = double.NaN,
            };

            SeedFirstValidValues(inputs, ref state);

            int warmupEnd = Math.Min(period, len);
            ProcessWarmupPhase(inputs, outputs, warmupEnd, factor, ref buffers, ref state);
            ProcessMainLoop(inputs, outputs, warmupEnd, period, factor, ref buffers, ref state);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedAdjHigh);
            ArrayPool<double>.Shared.Return(rentedAdjLow);
            ArrayPool<double>.Shared.Return(rentedClose);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SeedFirstValidValues(scoped BatchInputs inputs, ref ScalarState state)
    {
        int len = inputs.Close.Length;
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(inputs.High[k]) && double.IsNaN(state.LastValidHigh))
            {
                state.LastValidHigh = inputs.High[k];
            }

            if (double.IsFinite(inputs.Low[k]) && double.IsNaN(state.LastValidLow))
            {
                state.LastValidLow = inputs.Low[k];
            }

            if (double.IsFinite(inputs.Close[k]) && double.IsNaN(state.LastValidClose))
            {
                state.LastValidClose = inputs.Close[k];
            }

            if (!double.IsNaN(state.LastValidHigh) && !double.IsNaN(state.LastValidLow) && !double.IsNaN(state.LastValidClose))
            {
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double h, double l, double c) GetValidHLC(scoped BatchInputs inputs, int i, ref ScalarState state)
    {
        double h = inputs.High[i];
        double l = inputs.Low[i];
        double c = inputs.Close[i];

        if (double.IsFinite(h))
        {
            state.LastValidHigh = h;
        }
        else
        {
            h = state.LastValidHigh;
        }

        if (double.IsFinite(l))
        {
            state.LastValidLow = l;
        }
        else
        {
            l = state.LastValidLow;
        }

        if (double.IsFinite(c))
        {
            state.LastValidClose = c;
        }
        else
        {
            c = state.LastValidClose;
        }

        return (h, l, c);
    }

    /// <summary>
    /// Computes adjusted high/low per bar using Headley's formula and writes band outputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBandOutputs(scoped BatchOutputs outputs, int i, double smaAdjHigh, double smaAdjLow, double smaClose)
    {
        outputs.Middle[i] = smaClose;
        outputs.Upper[i] = smaAdjHigh;
        outputs.Lower[i] = smaAdjLow;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessWarmupPhase(
        scoped BatchInputs inputs,
        scoped BatchOutputs outputs,
        int warmupEnd,
        double factor,
        ref WorkBuffers buffers,
        ref ScalarState state)
    {
        for (int i = 0; i < warmupEnd; i++)
        {
            var (h, l, c) = GetValidHLC(inputs, i, ref state);

            // Headley's per-bar adjustment
            double denom = h + l;
            double w = denom != 0.0 ? (h - l) / denom : 0.0;
            double adjHigh = h * (1.0 + factor * w);
            double adjLow = l * (1.0 - factor * w);

            state.SumAdjHigh += adjHigh;
            state.SumAdjLow += adjLow;
            state.SumClose += c;

            buffers.AdjHigh[i] = adjHigh;
            buffers.AdjLow[i] = adjLow;
            buffers.Close[i] = c;

            int count = i + 1;
            WriteBandOutputs(outputs, i, state.SumAdjHigh / count, state.SumAdjLow / count, state.SumClose / count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessMainLoop(
        scoped BatchInputs inputs,
        scoped BatchOutputs outputs,
        int startIndex,
        int period,
        double factor,
        ref WorkBuffers buffers,
        ref ScalarState state)
    {
        int len = inputs.Close.Length;
        for (int i = startIndex; i < len; i++)
        {
            var (h, l, c) = GetValidHLC(inputs, i, ref state);

            // Headley's per-bar adjustment
            double denom = h + l;
            double w = denom != 0.0 ? (h - l) / denom : 0.0;
            double adjHigh = h * (1.0 + factor * w);
            double adjLow = l * (1.0 - factor * w);

            state.SumAdjHigh = state.SumAdjHigh - buffers.AdjHigh[state.BufferIndex] + adjHigh;
            state.SumAdjLow = state.SumAdjLow - buffers.AdjLow[state.BufferIndex] + adjLow;
            state.SumClose = state.SumClose - buffers.Close[state.BufferIndex] + c;

            buffers.AdjHigh[state.BufferIndex] = adjHigh;
            buffers.AdjLow[state.BufferIndex] = adjLow;
            buffers.Close[state.BufferIndex] = c;

            state.BufferIndex++;
            if (state.BufferIndex >= period)
            {
                state.BufferIndex = 0;
            }

            WriteBandOutputs(outputs, i, state.SumAdjHigh / period, state.SumAdjLow / period, state.SumClose / period);

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
        ReadOnlySpan<double> adjHighSpan = buffers.AdjHigh[..period];
        ReadOnlySpan<double> adjLowSpan = buffers.AdjLow[..period];
        ReadOnlySpan<double> closeSpan = buffers.Close[..period];
        state.SumAdjHigh = adjHighSpan.SumSIMD();
        state.SumAdjLow = adjLowSpan.SumSIMD();
        state.SumClose = closeSpan.SumSIMD();
    }

    /// <summary>
    /// Runs a high-performance batch calculation and returns a "Hot" AccBands instance.
    /// </summary>
    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, AccBands Indicator) Calculate(TBarSeries source, int period, double factor = 4.0)
    {
        var accBands = new AccBands(period, factor);
        var results = accBands.Update(source);
        return (results, accBands);
    }
}
