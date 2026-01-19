using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AtrBands: ATR Bands
/// </summary>
/// <remarks>
/// ATR Bands use Average True Range (ATR) to create adaptive bands around a simple
/// moving average of the source price. The bands expand during volatile periods
/// and contract during consolidation.
///
/// Calculation:
/// Middle Band = SMA(Source, Period)
/// ATR = RMA(True Range, Period) with warmup compensation
/// Upper Band = Middle + (Multiplier × ATR)
/// Lower Band = Middle - (Multiplier × ATR)
///
/// Key characteristics:
/// - Uses RMA with warmup compensator for ATR calculation
/// - Bands adapt to volatility via True Range
/// - O(1) complexity per update
///
/// Sources:
/// Based on concepts from J. Welles Wilder's ATR
/// </remarks>
[SkipLocalsInit]
public sealed class AtrBands : ITValuePublisher, IDisposable
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly double _alpha;
    private readonly double _decay;
    private readonly RingBuffer _sourceBuffer;
    private readonly TBarPublishedHandler _barHandler;
    private TBarSeries? _source;
    private bool _disposed;

    private const double ConvergenceThreshold = 1e-10;
    private const int ResyncInterval = 1000;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumSource,
        double RawRma,
        double E,
        double PrevClose,
        double LastValidSource,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        int TickCount
    )
    {
        public static State New() => new()
        {
            SumSource = 0,
            RawRma = 0,
            E = 1.0,
            PrevClose = double.NaN,
            LastValidSource = double.NaN,
            LastValidHigh = double.NaN,
            LastValidLow = double.NaN,
            LastValidClose = double.NaN,
            TickCount = 0,
        };
    }

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
    /// Creates AtrBands with specified period and multiplier.
    /// </summary>
    /// <param name="period">Lookback period for SMA and ATR calculations (must be > 0)</param>
    /// <param name="multiplier">Multiplier for band width (must be > 0, default: 2.0)</param>
    public AtrBands(int period, double multiplier = 2.0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (multiplier <= 0)
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));

        _period = period;
        _multiplier = multiplier;
        _alpha = 1.0 / period;
        _decay = 1.0 - _alpha;
        _sourceBuffer = new RingBuffer(period);
        Name = $"AtrBands({period},{multiplier:F2})";
        WarmupPeriod = period;
        _state = State.New();
        _p_state = _state;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates AtrBands with TBarSeries source.
    /// </summary>
    public AtrBands(TBarSeries source, int period, double multiplier = 2.0) : this(period, multiplier)
    {
        _source = source;
        Prime(source);
        source.Pub += _barHandler;
    }

    /// <summary>
    /// Releases managed resources (unsubscribes from source event).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_source is not null)
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
    /// Calculates True Range from OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateTrueRange(double high, double low, double prevClose)
    {
        if (double.IsNaN(prevClose))
            return high - low;

        double hl = high - low;
        double hpc = Math.Abs(high - prevClose);
        double lpc = Math.Abs(low - prevClose);
        return Math.Max(hl, Math.Max(hpc, lpc));
    }

    /// <summary>
    /// Updates the indicator with a TBar input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
            _p_state = _state;
        else
            _state = _p_state;

        // Get valid values with last-value substitution
        double source = input.Close;
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(source)) _state.LastValidSource = source; else source = _state.LastValidSource;
        if (double.IsFinite(high)) _state.LastValidHigh = high; else high = _state.LastValidHigh;
        if (double.IsFinite(low)) _state.LastValidLow = low; else low = _state.LastValidLow;
        if (double.IsFinite(close)) _state.LastValidClose = close; else close = _state.LastValidClose;

        // Handle first valid value initialization
        if (double.IsNaN(source))
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = new TValue(input.Time, double.NaN);
            Lower = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Calculate True Range
        double tr = CalculateTrueRange(high, low, _state.PrevClose);

        // Update SMA of source
        if (isNew)
        {
            double removed = _sourceBuffer.Count == _sourceBuffer.Capacity ? _sourceBuffer.Oldest : 0.0;
            _state.SumSource = _state.SumSource - removed + source;
            _sourceBuffer.Add(source);

            _state.TickCount++;
            if (_sourceBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.SumSource = _sourceBuffer.RecalculateSum();
            }
        }
        else
        {
            _sourceBuffer.UpdateNewest(source);
            _state.SumSource = _sourceBuffer.Sum;
        }

        // Calculate ATR using RMA with warmup compensation
        _state.RawRma = Math.FusedMultiplyAdd(_state.RawRma, _decay, _alpha * tr);
        _state.E *= _decay;

        double atr = _state.E > ConvergenceThreshold ? _state.RawRma / (1.0 - _state.E) : _state.RawRma;

        // Calculate bands
        int count = _sourceBuffer.Count;
        double middle = count > 0 ? _state.SumSource / count : source;
        double width = atr * _multiplier;

        if (isNew)
            _state.PrevClose = close;

        Last = new TValue(input.Time, middle);
        Upper = new TValue(input.Time, middle + width);
        Lower = new TValue(input.Time, middle - width);

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

        // Use batch calculation
        Batch(source.HighValues, source.LowValues, source.CloseValues,
              vMiddleSpan, vUpperSpan, vLowerSpan, _period, _multiplier);

        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        // Prime the state for continued streaming
        Prime(source);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    /// <summary>
    /// Initializes the indicator state using the provided TBarSeries history.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        if (source.Count == 0) return;

        // Reset state
        _sourceBuffer.Clear();
        _state = State.New();
        _p_state = _state;

        int warmupLength = Math.Min(source.Count, WarmupPeriod);
        int startIndex = source.Count - warmupLength;

        // Seed LastValidValues
        for (int i = startIndex - 1; i >= 0; i--)
        {
            var bar = source[i];
            if (double.IsFinite(bar.Close) && double.IsNaN(_state.LastValidSource))
            {
                _state.LastValidSource = bar.Close;
                _state.LastValidClose = bar.Close;
            }
            if (double.IsFinite(bar.High) && double.IsNaN(_state.LastValidHigh))
                _state.LastValidHigh = bar.High;
            if (double.IsFinite(bar.Low) && double.IsNaN(_state.LastValidLow))
                _state.LastValidLow = bar.Low;
            if (!double.IsNaN(_state.LastValidSource) && !double.IsNaN(_state.LastValidHigh) && !double.IsNaN(_state.LastValidLow))
                break;
        }

        // Find valid values in warmup window if not found
        if (double.IsNaN(_state.LastValidSource))
        {
            for (int i = startIndex; i < source.Count; i++)
            {
                var bar = source[i];
                if (double.IsFinite(bar.Close))
                {
                    _state.LastValidSource = bar.Close;
                    _state.LastValidClose = bar.Close;
                    _state.LastValidHigh = bar.High;
                    _state.LastValidLow = bar.Low;
                    break;
                }
            }
        }

        // Feed the data
        for (int i = startIndex; i < source.Count; i++)
        {
            _ = Update(source[i], isNew: true);
        }

        _p_state = _state;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    public void Reset()
    {
        _sourceBuffer.Clear();
        _state = State.New();
        _p_state = _state;
        Last = default;
        Upper = default;
        Lower = default;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Static Batch Methods
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Calculates AtrBands for the entire TBarSeries using a new instance.
    /// </summary>
    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period, double multiplier = 2.0)
    {
        var atrBands = new AtrBands(period, multiplier);
        return atrBands.Update(source);
    }

    /// <summary>
    /// Calculates AtrBands in-place using spans for maximum performance.
    /// </summary>
    /// <param name="input">Input spans containing High, Low, Close prices.</param>
    /// <param name="output">Output spans for Middle, Upper, Lower bands.</param>
    /// <param name="period">Lookback period for SMA and ATR calculations.</param>
    /// <param name="multiplier">Multiplier for band width (default: 2.0).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(AtrBandsInput input, AtrBandsOutput output, int period, double multiplier = 2.0)
    {
        int len = input.Close.Length;
        if (input.High.Length != len || input.Low.Length != len)
            throw new ArgumentException("High, Low, and Close must have the same length", nameof(input));
        if (output.Middle.Length < len || output.Upper.Length < len || output.Lower.Length < len)
            throw new ArgumentException("Output buffers must be at least as long as input", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (multiplier <= 0)
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));

        if (len == 0) return;

        CalculateScalarCore(input.High, input.Low, input.Close, output.Middle, output.Upper, output.Lower, period, multiplier);
    }

    /// <summary>
    /// Calculates AtrBands in-place using spans for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double multiplier = 2.0)
    {
        Batch(new AtrBandsInput(high, low, close), new AtrBandsOutput(middle, upper, lower), period, multiplier);
    }

    /// <summary>
    /// Input spans for ATR Bands calculation (High, Low, Close prices).
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct AtrBandsInput
    {
        /// <summary>High prices.</summary>
        public readonly ReadOnlySpan<double> High;
        /// <summary>Low prices.</summary>
        public readonly ReadOnlySpan<double> Low;
        /// <summary>Close prices.</summary>
        public readonly ReadOnlySpan<double> Close;

        /// <summary>Creates input from OHLC spans.</summary>
        public AtrBandsInput(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close)
        {
            High = high;
            Low = low;
            Close = close;
        }
    }

    /// <summary>
    /// Output spans for ATR Bands calculation (Middle, Upper, Lower bands).
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct AtrBandsOutput
    {
        /// <summary>Middle band (SMA of source).</summary>
        public readonly Span<double> Middle;
        /// <summary>Upper band.</summary>
        public readonly Span<double> Upper;
        /// <summary>Lower band.</summary>
        public readonly Span<double> Lower;

        /// <summary>Creates output from band spans.</summary>
        public AtrBandsOutput(Span<double> middle, Span<double> upper, Span<double> lower)
        {
            Middle = middle;
            Upper = upper;
            Lower = lower;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> middleOut,
        Span<double> upperOut,
        Span<double> lowerOut,
        int period,
        double multiplier)
    {
        int len = close.Length;
        double alpha = 1.0 / period;
        double decay = 1.0 - alpha;

        // Rent buffer for SMA calculation
        double[] rentedBuffer = ArrayPool<double>.Shared.Rent(period);
        try
        {
            Span<double> sourceBuffer = rentedBuffer.AsSpan(0, period);
            sourceBuffer.Clear();

            double sumSource = 0;
            double rawRma = 0;
            double e = 1.0;
            double prevClose = double.NaN;
            double lastValidHigh = double.NaN;
            double lastValidLow = double.NaN;
            double lastValidClose = double.NaN;
            int bufferIndex = 0;
            int count = 0;

            // Seed first valid values
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(close[k]))
                {
                    lastValidClose = close[k];
                    lastValidHigh = high[k];
                    lastValidLow = low[k];
                    break;
                }
            }

            for (int i = 0; i < len; i++)
            {
                double h = high[i];
                double l = low[i];
                double c = close[i];

                // Get valid values
                if (double.IsFinite(h)) lastValidHigh = h; else h = lastValidHigh;
                if (double.IsFinite(l)) lastValidLow = l; else l = lastValidLow;
                if (double.IsFinite(c)) lastValidClose = c; else c = lastValidClose;

                if (double.IsNaN(c))
                {
                    middleOut[i] = double.NaN;
                    upperOut[i] = double.NaN;
                    lowerOut[i] = double.NaN;
                    continue;
                }

                // Calculate True Range
                double tr;
                if (double.IsNaN(prevClose))
                    tr = h - l;
                else
                {
                    double hl = h - l;
                    double hpc = Math.Abs(h - prevClose);
                    double lpc = Math.Abs(l - prevClose);
                    tr = Math.Max(hl, Math.Max(hpc, lpc));
                }

                // Update SMA of source (close)
                if (count < period)
                {
                    sumSource += c;
                    sourceBuffer[count] = c;
                    count++;
                }
                else
                {
                    sumSource = sumSource - sourceBuffer[bufferIndex] + c;
                    sourceBuffer[bufferIndex] = c;
                    bufferIndex = (bufferIndex + 1) % period;
                }

                // Calculate ATR using RMA with warmup compensation
                rawRma = Math.FusedMultiplyAdd(rawRma, decay, alpha * tr);
                e *= decay;

                double atr = e > ConvergenceThreshold ? rawRma / (1.0 - e) : rawRma;

                // Calculate bands
                double mid = sumSource / count;
                double width = atr * multiplier;

                middleOut[i] = mid;
                upperOut[i] = mid + width;
                lowerOut[i] = mid - width;

                prevClose = c;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Runs a high-performance batch calculation and returns a "Hot" AtrBands instance.
    /// </summary>
    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, AtrBands Indicator) Calculate(TBarSeries source, int period, double multiplier = 2.0)
    {
        var atrBands = new AtrBands(period, multiplier);
        var results = atrBands.Update(source);
        return (results, atrBands);
    }
}