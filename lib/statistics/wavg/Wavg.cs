using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Wavg: Rolling Linearly-Weighted Average
/// </summary>
/// <remarks>
/// Assigns linearly increasing weights to the lookback window:
///   weight_i = i + 1 for i = 0 (oldest) to count-1 (newest)
///   WAVG = Σ(weight_i × value_i) / Σ(weight_i)
///   Σ(weight_i) = count × (count + 1) / 2
///
/// O(1) incremental update uses two recurrences:
///
/// WARMUP (count growing 1 → period):
///   W_new = W_old + count_new × v_new        (no subtraction; existing positions unchanged)
///   S_new = S_old + v_new
///
/// STEADY STATE (window full, oldest departs):
///   W_new = W_old - S_old + period × v_new   (shift all weights down, evict oldest, add new)
///   S_new = S_old - oldest + v_new
///
/// Mathematically identical to WMA.
/// </remarks>
[SkipLocalsInit]
public sealed class Wavg : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;

    // O(1) running state
    private double _weightedSum;
    private double _runningSum;
    private int _count;
    private double _lastValidValue;

    // Previous-state snapshot for isNew=false rollback
    private double _p_weightedSum;
    private double _p_runningSum;
    private int _p_count;

    private bool _disposed;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a Wavg indicator with the specified period.
    /// </summary>
    /// <param name="period">The size of the rolling window (must be > 0).</param>
    public Wavg(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Wavg({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>Creates a chained Wavg indicator.</summary>
    public Wavg(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    /// <summary>Creates a Wavg indicator primed from a TSeries source.</summary>
    public Wavg(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }

        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            _lastValidValue = value;
        }

        if (isNew)
        {
            // Save state for potential rollback
            _p_weightedSum = _weightedSum;
            _p_runningSum = _runningSum;
            _p_count = _count;

            if (_buffer.IsFull)
            {
                // STEADY STATE: oldest departs
                // Shift all weights down by 1 (each existing element's weight decreases by 1,
                // so δW = -S_old). Then evict oldest from S. Then add new at weight = period.
                _weightedSum -= _runningSum;     // shift: δW = -S_old (oldest contribution zeroes out)
                _runningSum -= _buffer.Oldest;   // evict oldest from unweighted sum
                _runningSum += value;
                _weightedSum += _count * value;  // add new at weight = period (= _count, fixed when full)
            }
            else
            {
                // WARMUP: no eviction, existing positions unchanged, new element appended at weight = count+1
                _count++;
                _runningSum += value;
                _weightedSum += _count * value;
            }

            _buffer.Add(value);
        }
        else
        {
            // Bar correction: restore previous state, then replace newest in buffer and recompute
            // O(period) recompute — only triggered on bar corrections, not the hot path
            _weightedSum = _p_weightedSum;
            _runningSum = _p_runningSum;
            _count = _p_count;

            // Undo the last Add of the old newest value (before the prior isNew=true step)
            double oldNewest = _buffer.Newest;

            if (_count == _period)
            {
                // The prior step was steady-state: undo it, then redo with new value
                // Undo: W = W_p, S = S_p (already restored from _p_)
                // Redo steady-state with different new value:
                _weightedSum -= _runningSum;
                _runningSum -= _buffer.Oldest;
                _runningSum += value;
                _weightedSum += _count * value;
            }
            else
            {
                // The prior step was warmup: undo newest contribution, sub in corrected value
                // _count was already incremented in the prior isNew=true step, so _p_count = _count-1
                // After restoring _count = _p_count, reapply the warmup step with new value
                _count++;
                _runningSum -= oldNewest;
                _runningSum += value;
                _weightedSum -= _count * oldNewest;
                _weightedSum += _count * value;
            }

            // Note: buffer is NOT rolled back on isNew=false — UpdateNewest replaces in-place
            _buffer.UpdateNewest(value);
        }

        double denom = _count * (_count + 1.0) / 2.0;
        double result = denom > 0.0 ? _weightedSum / denom : value;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

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

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _weightedSum = 0;
        _runningSum = 0;
        _count = 0;
        _p_weightedSum = 0;
        _p_runningSum = 0;
        _p_count = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _buffer.Clear();
        _weightedSum = 0;
        _runningSum = 0;
        _count = 0;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]));
        }
    }

    /// <summary>Calculates Wavg for the entire series using a new instance.</summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var wavg = new Wavg(period);
        return wavg.Update(source);
    }

    /// <summary>Calculates Wavg in-place using spans. O(n) total, O(1) per bar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Circular buffer for oldest-value eviction
        double[] buf = new double[period];
        int head = 0;
        double weightedSum = 0.0;
        double runningSum = 0.0;
        int count = 0;

        for (int i = 0; i < len; i++)
        {
            double v = source[i];

            if (count < period)
            {
                // WARMUP: append, existing weights unchanged
                count++;
                runningSum += v;
                weightedSum += count * v;
            }
            else
            {
                // STEADY STATE: shift all weights down, evict oldest, add new at weight=period
                double oldest = buf[head];
                weightedSum -= runningSum;     // shift: each existing weight -1
                runningSum -= oldest;           // evict oldest
                runningSum += v;
                weightedSum += count * v;       // add new at weight=period (=count, fixed)
            }

            buf[head] = v;
            head = (head + 1) % period;

            double denom = count * (count + 1.0) / 2.0;
            output[i] = denom > 0.0 ? weightedSum / denom : v;
        }
    }

    public static (TSeries Results, Wavg Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Wavg(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
