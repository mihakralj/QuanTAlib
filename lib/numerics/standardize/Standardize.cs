// STANDARDIZE: Z-Score Normalization
// Calculates the z-score (standard score) of values over a lookback period
// Formula: z = (x - μ) / σ  where σ uses sample standard deviation (N-1)

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// STANDARDIZE: Z-Score Normalization
/// Calculates the z-score of values over a lookback period using sample standard deviation.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output is unbounded (can be any real number, typically -3 to +3 for normal data)
/// - Uses sample standard deviation (Bessel's correction, N-1 denominator)
/// - Requires period >= 2 for meaningful standard deviation calculation
/// - When stdev is zero (flat data), returns 0 if value equals mean, NaN otherwise
/// - Commonly used for anomaly detection and inter-series comparison
///
/// Formula: z = (x - mean) / sample_stdev
/// where sample_stdev = sqrt(sum((x_i - mean)^2) / (N - 1))
/// </remarks>
[SkipLocalsInit]
public sealed class Standardize : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    // Welford's online algorithm state for numerical stability
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidZScore, double Sum, double SumSq, int ValidCount);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Standardize indicator with specified lookback period.
    /// </summary>
    /// <param name="period">Lookback period for z-score calculation (default 20, must be >= 2)</param>
    public Standardize(int period = 20)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2 for sample standard deviation", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Standardize({period})";
        WarmupPeriod = period;
        _state = new State(0.0, 0.0, 0.0, 0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Standardize indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback period (default 20)</param>
    public Standardize(ITValuePublisher source, int period = 20) : this(period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double value = input.Value;
        double result;

        if (double.IsFinite(value))
        {
            _buffer.Add(value, isNew);

            // Compute mean and sample variance from buffer
            ReadOnlySpan<double> data = _buffer.GetSpan();
            int n = data.Length;

            if (n < 2)
            {
                // Not enough data for sample stdev
                result = 0.0;
                _state = new State(result, value, value * value, 1);
            }
            else
            {
                // Calculate sum and sum of squares
                double sum = 0.0;
                double sumSq = 0.0;

                for (int i = 0; i < n; i++)
                {
                    double v = data[i];
                    sum += v;
                    sumSq += v * v;
                }

                double mean = sum / n;

                // Population variance: (sumSq / n) - mean^2
                // Sample variance: (sumSq - n * mean^2) / (n - 1) = n / (n-1) * popVar
                double popVariance = (sumSq / n) - (mean * mean);

                // Numerical stability: clamp tiny negative values to zero
                if (popVariance < 1e-10)
                {
                    popVariance = 0.0;
                }

                double sampleVariance = popVariance * n / (n - 1);
                double stdev = Math.Sqrt(sampleVariance);

                if (stdev > 1e-10)
                {
                    result = (value - mean) / stdev;
                }
                else
                {
                    // Stdev is essentially zero - all values are the same
                    // Return 0 as neutral z-score
                    result = 0.0;
                }

                _state = new State(result, sum, sumSq, n);
            }
        }
        else
        {
            // Invalid input - return last valid z-score
            result = _state.LastValidZScore;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries(source.Count);
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), values[i]), true);
            result.Add(tv, true);
        }
        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Calculate(TSeries source, int period = 20)
    {
        var indicator = new Standardize(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Z-score normalization over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 20)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        double lastValid = 0.0;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];

            if (!double.IsFinite(val))
            {
                output[i] = lastValid;
                continue;
            }

            // Determine window bounds
            int start = Math.Max(0, i - period + 1);
            int n = 0;
            double sum = 0.0;
            double sumSq = 0.0;

            // Calculate sum and count of finite values in window
            for (int j = start; j <= i; j++)
            {
                double v = source[j];
                if (double.IsFinite(v))
                {
                    sum += v;
                    sumSq += v * v;
                    n++;
                }
            }

            if (n < 2)
            {
                output[i] = 0.0;
                lastValid = 0.0;
                continue;
            }

            double mean = sum / n;
            double popVariance = (sumSq / n) - (mean * mean);

            if (popVariance < 1e-10)
            {
                popVariance = 0.0;
            }

            double sampleVariance = popVariance * n / (n - 1);
            double stdev = Math.Sqrt(sampleVariance);

            double result;
            if (stdev > 1e-10)
            {
                result = (val - mean) / stdev;
            }
            else
            {
                result = 0.0;
            }

            lastValid = result;
            output[i] = result;
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(0.0, 0.0, 0.0, 0);
        _p_state = _state;
        Last = default;
    }
}
