using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RWMA: Range Weighted Moving Average
/// </summary>
/// <remarks>
/// Weights each bar's contribution by its price range (high - low), giving
/// greater influence to volatile bars and less to narrow-range bars.
/// <c>RWMA = Σ(close_i × range_i) / Σ(range_i)</c> where <c>range_i = max(high_i - low_i, 0)</c>.
///
/// Requires TBar (OHLC) inputs. When all bars have zero range the output
/// degenerates to the current close price.
///
/// O(1) per bar via circular buffers with running sums.
/// </remarks>
/// <seealso href="Rwma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Rwma : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SumCR, double SumR, int Index, int Head, int Count, int SyncCounter)
    {
        public static State New() => new() { SumCR = 0, SumR = 0, Index = 0, Head = 0, Count = 0, SyncCounter = 0 };
    }

    /// <summary>
    /// Resync interval to limit floating-point drift in running sums.
    /// Full recalculation every N bars.
    /// </summary>
    private const int ResyncInterval = 1000;

    private readonly int _period;
    private readonly double[] _closeBuffer;
    private readonly double[] _rangeBuffer;
    private State _state;
    private State _p_state;
    private double _lastValidClose;
    private double _lastValidHigh;
    private double _lastValidLow;
    private double _p_lastValidClose;
    private double _p_lastValidHigh;
    private double _p_lastValidLow;
    private double _p_bufferClose;
    private double _p_bufferRange;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current RWMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed at least Period bars.
    /// </summary>
    public bool IsHot => _state.Count >= _period;

    /// <summary>
    /// Warmup period equals the specified period.
    /// </summary>
#pragma warning disable S2325
    public int WarmupPeriod => _period;
#pragma warning restore S2325

    /// <summary>
    /// Creates a new RWMA indicator.
    /// </summary>
    /// <param name="period">Lookback period. Must be >= 1.</param>
    public Rwma(int period = 14)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _closeBuffer = new double[period];
        _rangeBuffer = new double[period];
        _state = State.New();
        _p_state = State.New();
        Name = $"Rwma({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = State.New();
        _p_state = State.New();
        Array.Clear(_closeBuffer);
        Array.Clear(_rangeBuffer);
        _lastValidClose = 0;
        _lastValidHigh = 0;
        _lastValidLow = 0;
        _p_lastValidClose = 0;
        _p_lastValidHigh = 0;
        _p_lastValidLow = 0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(double input, ref double lastValid)
    {
        if (double.IsFinite(input))
        {
            lastValid = input;
            return input;
        }
        return lastValid;
    }

    /// <summary>
    /// Recalculates running sums from buffer to eliminate accumulated floating-point drift.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResyncRunningTotals(ref State s)
    {
        double sumCR = 0;
        double sumR = 0;

        for (int i = 0; i < _period; i++)
        {
            double c = _closeBuffer[i];
            double r = _rangeBuffer[i];
            sumCR += c * r;
            sumR += r;
        }

        s.SumCR = sumCR;
        s.SumR = sumR;
    }

    /// <summary>
    /// Updates RWMA with a TBar input (uses close, high, low).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, bool isNew = true)
    {
        return UpdateInternal(input.Time, input.Close, input.High, input.Low, isNew);
    }

    /// <summary>
    /// Updates RWMA with a TValue input (uses value as close, range = 0).
    /// With zero range all bars have equal weight, degenerating to SMA.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // When given a single value, high = low = close → range = 0
        // All weights are 0, so fallback to current close
        return UpdateInternal(input.Time, input.Value, input.Value, input.Value, isNew);
    }

    /// <summary>
    /// Calculates RWMA for an entire bar series.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private TValue UpdateInternal(long time, double close, double high, double low, bool isNew)
    {
        var s = _state;

        if (isNew)
        {
            _p_state = _state;
            _p_lastValidClose = _lastValidClose;
            _p_lastValidHigh = _lastValidHigh;
            _p_lastValidLow = _lastValidLow;
            _p_bufferClose = _closeBuffer[s.Head];
            _p_bufferRange = _rangeBuffer[s.Head];
        }
        else
        {
            s = _p_state;
            _state = _p_state;
            _lastValidClose = _p_lastValidClose;
            _lastValidHigh = _p_lastValidHigh;
            _lastValidLow = _p_lastValidLow;
            _closeBuffer[s.Head] = _p_bufferClose;
            _rangeBuffer[s.Head] = _p_bufferRange;
        }

        double currentClose = GetValidValue(close, ref _lastValidClose);
        double currentHigh = GetValidValue(high, ref _lastValidHigh);
        double currentLow = GetValidValue(low, ref _lastValidLow);
        double currentRange = Math.Max(currentHigh - currentLow, 0.0);

        // Remove old values from circular buffer
        double oldClose = _closeBuffer[s.Head];
        double oldRange = _rangeBuffer[s.Head];

        if (s.Count >= _period)
        {
            s.SumCR -= oldClose * oldRange;
            s.SumR -= oldRange;
        }

        // Add new values
        s.SumCR += currentClose * currentRange;
        s.SumR += currentRange;

        // Store in circular buffer
        _closeBuffer[s.Head] = currentClose;
        _rangeBuffer[s.Head] = currentRange;

        // Advance head pointer
        s.Head = (s.Head + 1) % _period;

        if (isNew)
        {
            s.Index++;
            if (s.Count < _period)
            {
                s.Count++;
            }

            // Periodic resync to limit floating-point drift
            s.SyncCounter++;
            if (s.SyncCounter >= ResyncInterval && s.Count >= _period)
            {
                s.SyncCounter = 0;
                ResyncRunningTotals(ref s);
            }
        }

        // Calculate RWMA: Σ(close × range) / Σ(range)
        // When all ranges are zero, fall back to current close
        double rwma = s.SumR > double.Epsilon ? s.SumCR / s.SumR : currentClose;

        _state = s;

        Last = new TValue(time, rwma);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
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

    /// <summary>
    /// Static calculation returning TSeries from TBarSeries.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.Close.Values, source.High.Values, source.Low.Values, v, period);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Static calculation for TSeries (single-valued, range = 0 → degenerates to SMA).
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 14)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Times.ToArray();
        var v = new double[source.Count];

        // No high/low available — use close for high and low → range = 0, so always fallback to close
        Batch(source.Values, source.Values, source.Values, v, period);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Zero-allocation span-based calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int period = 14)
    {
        if (close.Length != high.Length || close.Length != low.Length)
        {
            throw new ArgumentException("Close, High, and Low spans must be of the same length", nameof(high));
        }

        if (close.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        int len = close.Length;
        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;
        double[]? rentedClose = null;
        double[]? rentedRange = null;
        scoped Span<double> closeBuffer;
        scoped Span<double> rangeBuffer;

        if (period <= StackallocThreshold)
        {
            closeBuffer = stackalloc double[period];
            rangeBuffer = stackalloc double[period];
        }
        else
        {
            rentedClose = System.Buffers.ArrayPool<double>.Shared.Rent(period);
            rentedRange = System.Buffers.ArrayPool<double>.Shared.Rent(period);
            closeBuffer = rentedClose.AsSpan(0, period);
            rangeBuffer = rentedRange.AsSpan(0, period);
        }

        try
        {
            closeBuffer.Clear();
            rangeBuffer.Clear();

            double sumCR = 0;
            double sumR = 0;
            double lastValidClose = 0;
            double lastValidHigh = 0;
            double lastValidLow = 0;
            int head = 0;
            int count = 0;

            // Find first valid values
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(close[k])) { lastValidClose = close[k]; break; }
            }
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(high[k])) { lastValidHigh = high[k]; break; }
            }
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(low[k])) { lastValidLow = low[k]; break; }
            }

            int syncCounter = 0;

            for (int i = 0; i < len; i++)
            {
                double currentClose = double.IsFinite(close[i]) ? close[i] : lastValidClose;
                double currentHigh = double.IsFinite(high[i]) ? high[i] : lastValidHigh;
                double currentLow = double.IsFinite(low[i]) ? low[i] : lastValidLow;

                if (double.IsFinite(close[i]))
                {
                    lastValidClose = close[i];
                }
                if (double.IsFinite(high[i]))
                {
                    lastValidHigh = high[i];
                }
                if (double.IsFinite(low[i]))
                {
                    lastValidLow = low[i];
                }

                double currentRange = Math.Max(currentHigh - currentLow, 0.0);

                // Remove old values from circular buffer
                double oldClose = closeBuffer[head];
                double oldRange = rangeBuffer[head];

                if (count >= period)
                {
                    sumCR -= oldClose * oldRange;
                    sumR -= oldRange;
                }

                // Add new values
                sumCR += currentClose * currentRange;
                sumR += currentRange;

                // Store in circular buffer
                closeBuffer[head] = currentClose;
                rangeBuffer[head] = currentRange;

                head = (head + 1) % period;

                if (count < period)
                {
                    count++;
                }

                // Periodic resync
                syncCounter++;
                if (syncCounter >= ResyncInterval && count >= period)
                {
                    syncCounter = 0;
                    sumCR = 0;
                    sumR = 0;
                    for (int j = 0; j < period; j++)
                    {
                        sumCR += closeBuffer[j] * rangeBuffer[j];
                        sumR += rangeBuffer[j];
                    }
                }

                output[i] = sumR > double.Epsilon ? sumCR / sumR : currentClose;
            }
        }
        finally
        {
            if (rentedClose != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedClose);
            }
            if (rentedRange != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedRange);
            }
        }
    }

    public static (TSeries Results, Rwma Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Rwma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
