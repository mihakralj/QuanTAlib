using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BLMA: Blackman Moving Average
/// A weighted moving average using the Blackman window function for smoother transitions.
/// </summary>
[SkipLocalsInit]
public sealed class Blma : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly double[] _weights;
    private readonly double _weightSum;

    public override bool IsHot => _buffer.Count >= _period;

    public Blma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0");
        }

        _period = period;
        Name = $"Blma({period})";
        WarmupPeriod = period;
        _buffer = new RingBuffer(period);
        _weights = new double[period];

        // Pre-calculate weights for the full period
        _weightSum = CalculateWeights(period, _weights);
    }

    public Blma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Reset()
    {
        _buffer.Clear();
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan increment = step ?? TimeSpan.FromMilliseconds(1);
        DateTime time = DateTime.UtcNow;
        foreach (var value in source)
        {
            Update(new TValue(time, value));
            time = time.Add(increment);
        }
    }

    public void Prime(ReadOnlySpan<TValue> source)
    {
        foreach (var value in source)
        {
            Update(value);
        }
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        // Handle NaN/Infinity - return last result without changing state
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            return Last;
        }

        _buffer.Add(val, isNew);

        double result;
        if (_buffer.Count < _period)
        {
            // During warmup, calculate weights dynamically for the current count
            int count = _buffer.Count;
            if (count == 1)
            {
                result = val;
            }
            else
            {
                Span<double> currentWeights = stackalloc double[count];
                double currentWeightSum = CalculateWeights(count, currentWeights);
                result = ComputeWeightedAverage(
                    currentWeightSum,
                    CalculateWeightedSum(_buffer, currentWeights),
                    _buffer.Average());
            }
        }
        else
        {
            // Full period, use pre-calculated weights
            result = ComputeWeightedAverage(
                _weightSum,
                CalculateWeightedSum(_buffer, _weights),
                _buffer.Average());
        }

        var tValue = new TValue(input.Time, result);
        Last = tValue;
        PubEvent(tValue, isNew);
        return tValue;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries();
        Span<double> output = new double[source.Count];
        Calculate(source.Values, output, _period);

        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(source[i].Time, output[i]));
        }

        // Restore state by replaying last Period bars
        // This ensures the indicator is ready for subsequent streaming updates
        Reset();
        int start = Math.Max(0, source.Count - _period);
        for (int i = start; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return result;
    }

    /// <summary>
    /// Computes weighted average with fallback for zero weight sum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeWeightedAverage(double weightSum, double weightedSum, double fallbackAverage)
    {
        return Math.Abs(weightSum) < double.Epsilon ? fallbackAverage : weightedSum / weightSum;
    }

    private static double CalculateWeights(int n, Span<double> weights)
    {
        if (n == 1)
        {
            weights[0] = 1.0;
            return 1.0;
        }

        double totalWeight = 0;
        double invNMinus1 = 1.0 / (n - 1);
        double pi2 = 2.0 * Math.PI;
        double pi4 = 4.0 * Math.PI;

        // Blackman window coefficients
        const double a0 = 0.42;
        const double a1 = 0.5;
        const double a2 = 0.08;

        for (int i = 0; i < n; i++)
        {
            double ratio = i * invNMinus1;
            double w = a0 - (a1 * Math.Cos(pi2 * ratio)) + (a2 * Math.Cos(pi4 * ratio));
            weights[i] = w;
            totalWeight += w;
        }

        return totalWeight;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateWeightedSum(RingBuffer buffer, ReadOnlySpan<double> weights)
    {
        int start = buffer.StartIndex;
        int count = buffer.Count;
        int capacity = buffer.Capacity;

        if (start + count <= capacity)
        {
            return buffer.InternalBuffer.Slice(start, count).DotProduct(weights);
        }

        int firstPartLength = capacity - start;
        int secondPartLength = count - firstPartLength;

        double sum1 = buffer.InternalBuffer.Slice(start, firstPartLength).DotProduct(weights[..firstPartLength]);
        double sum2 = buffer.InternalBuffer.Slice(0, secondPartLength).DotProduct(weights[firstPartLength..]);

        return sum1 + sum2;
    }

<<<<<<< HEAD
#pragma warning disable S3776 // Cognitive Complexity: necessary for SIMD optimization and edge case handling
=======
    /// <summary>
    /// Calculates BLMA values for a TSeries and returns both results and a primed indicator.
    /// </summary>
    public static (TSeries Results, Blma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Blma(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Calculates BLMA values using spans (high-performance batch API).
    /// </summary>
>>>>>>> d493bfd42fe5d6238736660aaaa808279cb3a27a
    public static void Calculate(ReadOnlySpan<double> source, Span<double> destination, int period)
    {
#pragma warning restore S3776
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0");
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), $"Destination length must be at least {source.Length}.");
        }

        // Pre-calculate weights for full period
        Span<double> weights = period <= 256 ? stackalloc double[period] : new double[period];
        double weightSum = CalculateWeights(period, weights);

        // Buffer for warmup weights to avoid stackalloc in loop
        Span<double> warmupWeightsBuffer = period <= 256 ? stackalloc double[period] : new double[period];

        // Handle NaN via last-valid-value substitution
        double lastValid = double.NaN;
        for (int i = 0; i < source.Length; i++)
        {
            if (double.IsFinite(source[i]))
            {
                lastValid = source[i];
                break;
            }
        }

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = double.IsNaN(lastValid) ? 0 : lastValid;
            }
            else
            {
                lastValid = val;
            }

            int count = Math.Min(i + 1, period);

            if (count < period)
            {
                // Warmup: dynamic weights
                if (count == 1)
                {
                    destination[i] = val;
                }
                else
                {
                    Span<double> currentWeights = warmupWeightsBuffer.Slice(0, count);
                    double currentWeightSum = CalculateWeights(count, currentWeights);

                    double sum = 0;
                    for (int j = 0; j < count; j++)
                    {
                        int srcIdx = i - count + 1 + j;
                        double srcVal = source[srcIdx];
                        if (!double.IsFinite(srcVal)) srcVal = lastValid;
                        sum += srcVal * currentWeights[j];
                    }

                    double avg = 0;
                    for (int j = 0; j < count; j++)
                    {
                        int srcIdx = i - count + 1 + j;
                        double srcVal = source[srcIdx];
                        if (!double.IsFinite(srcVal)) srcVal = lastValid;
                        avg += srcVal;
                    }
                    avg /= count;

                    destination[i] = ComputeWeightedAverage(currentWeightSum, sum, avg);
                }
            }
            else
            {
                // Full period
                double sum = 0;
                double avg = 0;
                for (int j = 0; j < period; j++)
                {
                    int srcIdx = i - period + 1 + j;
                    double srcVal = source[srcIdx];
                    if (!double.IsFinite(srcVal)) srcVal = lastValid;
                    sum += srcVal * weights[j];
                    avg += srcVal;
                }
                avg /= period;

                destination[i] = ComputeWeightedAverage(weightSum, sum, avg);
            }
        }
    }

    /// <summary>
    /// Batch calculates BLMA values for a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Blma(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculates BLMA values using spans.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> destination, int period)
    {
        Calculate(source, destination, period);
    }
}
