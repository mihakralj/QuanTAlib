using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CG: Center of Gravity - Ehlers' oscillator that identifies potential turning points
/// in a time series by calculating the weighted center of mass of prices.
/// </summary>
/// <remarks>
/// The Center of Gravity indicator, developed by John Ehlers, oscillates around zero
/// and provides early signals of potential reversals. It leads price movement,
/// making it useful for timing entries and exits.
///
/// Formula:
/// num = Î£(count * price[count-1]) for count = 1 to length
/// den = Î£(price[count-1]) for count = 1 to length
/// CG = (num / den) - (length + 1) / 2
///
/// Properties:
/// - Oscillates around zero
/// - Leads price movement (low lag)
/// - Positive values suggest downward pressure
/// - Negative values suggest upward pressure
/// - Zero crossings can signal turning points
///
/// Key Insight:
/// When prices are higher at the beginning of the window, CG is negative.
/// When prices are higher at the end of the window, CG is positive.
/// The indicator essentially measures where the "weight" of prices is concentrated.
/// </remarks>
[SkipLocalsInit]
public sealed class Cg : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    // Running sums for O(1) updates
    private double _weightedSum;
    private double _sum;

    // Snapshot state for bar correction
    private double _p_weightedSum;
    private double _p_sum;

    private int _updateCount;
    private const int ResyncInterval = 1000;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Center of Gravity indicator.
    /// </summary>
    /// <param name="period">The lookback period for calculating CG (must be > 0).</param>
    public Cg(int period = 10)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 1.");
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Cg({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates a chained Center of Gravity indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    /// <param name="period">The lookback period.</param>
    public Cg(ITValuePublisher source, int period = 10) : this(period)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = _buffer.Count > 0 ? _buffer.Newest : 0;
        }

        if (isNew)
        {
            // Snapshot state for rollback
            _p_weightedSum = _weightedSum;
            _p_sum = _sum;
            _buffer.Snapshot();
        }
        else
        {
            // Restore state from snapshot
            _weightedSum = _p_weightedSum;
            _sum = _p_sum;
            _buffer.Restore();
        }

        // Add new value to buffer
        _buffer.Add(value);

        // Recalculate running sums
        // Since the weights change position as we add values, we need to recalculate
        // after each update (or track differential updates which is complex)
        RecalculateSums();

        if (isNew)
        {
            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                RecalculateSums(); // Already done above, but keeps pattern consistent
            }
        }

        // Calculate CG
        double cg = CalculateCg();

        Last = new TValue(input.Time, cg);
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

        // Prime state with last 'period' values
        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateSums()
    {
        int n = _buffer.Count;
        _weightedSum = 0;
        _sum = 0;

        for (int i = 0; i < n; i++)
        {
            double price = _buffer[i];
            int weight = i + 1; // count = 1 to length (1-based weighting)
            _weightedSum += weight * price;
            _sum += price;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateCg()
    {
        int n = _buffer.Count;
        if (n == 0 || _sum == 0) // skipcq: CS-R1077 - Exact-zero guard: _sum is cumulative price sum; zero means empty buffer, division by zero below
        {
            return 0;
        }

        // CG = (weightedSum / sum) - (n + 1) / 2
        double centerOfMass = _weightedSum / _sum;
        double midpoint = (n + 1) / 2.0;
        return centerOfMass - midpoint;
    }

    public override void Reset()
    {
        _buffer.Clear();
        _weightedSum = 0;
        _sum = 0;
        _p_weightedSum = 0;
        _p_sum = 0;
        _updateCount = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Calculates CG for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 10)
    {
        var cg = new Cg(period);
        return cg.Update(source);
    }

    /// <summary>
    /// Calculates CG in-place using a pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 10)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 1.");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, Cg Indicator) Calculate(TSeries source, int period = 10)
    {
        var indicator = new Cg(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        int bufferIndex = 0;
        int bufferCount = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = bufferCount > 0 ? buffer[(bufferIndex - 1 + period) % period] : 0;
            }

            // Add to circular buffer
            if (bufferCount < period)
            {
                buffer[bufferCount] = val;
                bufferCount++;
            }
            else
            {
                buffer[bufferIndex] = val;
                bufferIndex = (bufferIndex + 1) % period;
            }

            double weightedSum = 0;
            double sum = 0;

            // Calculate sums over the current buffer
            int effectiveStart = bufferCount < period ? 0 : bufferIndex;

            for (int j = 0; j < bufferCount; j++)
            {
                int idx = (effectiveStart + j) % period;
                double price = buffer[idx];
                int weight = j + 1; // 1-based weighting
                weightedSum += weight * price;
                sum += price;
            }

            if (sum == 0) // skipcq: CS-R1077 - Exact-zero guard: sum is cumulative price sum; zero means empty buffer, division by zero below
            {
                output[i] = 0;
                continue;
            }

            double centerOfMass = weightedSum / sum;
            double midpoint = (bufferCount + 1) / 2.0;
            output[i] = centerOfMass - midpoint;
        }
    }
}