// NORMDIST: Normal Distribution CDF
// Applies the Gaussian CDF Φ(z) = 0.5*(1+erf(z/√2)) to a z-score normalized
// price series over a rolling lookback window.
// Pipeline: Rolling mean+stddev → z-score → parameter adjustment → erf approximation → CDF.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NORMDIST: Normal Distribution CDF
/// Computes Φ(z; μ, σ) = 0.5*(1+erf((z-μ)/(σ*√2))) applied to a z-score normalized
/// price series over a rolling lookback window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window computes mean and population stddev for z-score normalization
/// - Default μ=0, σ=1 gives standard-normal CDF of the price's z-score relative to the window
/// - Increasing σ compresses the S-curve; shifting μ moves the midpoint away from the rolling mean
/// - erf approximation: Abramowitz &amp; Stegun 7.1.25 (3-term), max error ~2.5e-5
/// - Fewer than 2 valid values in window → output 0.5 (uncertainty)
/// - NaN/Infinity inputs use last-valid-value substitution
/// </remarks>
[SkipLocalsInit]
public sealed class Normdist : AbstractBase
{
    private readonly int _period;
    private readonly double _mu;
    private readonly double _invSigmaSqrt2;   // precomputed: 1 / (sigma * sqrt(2))
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Normdist indicator.
    /// </summary>
    /// <param name="mu">Mean shift μ applied after z-score (default 0.0)</param>
    /// <param name="sigma">Scale σ &gt; 0 applied after z-score (default 1.0)</param>
    /// <param name="period">Lookback window for rolling z-score normalization (default 14)</param>
    public Normdist(double mu = 0.0, double sigma = 1.0, int period = 14)
    {
        if (sigma <= 0.0)
        {
            throw new ArgumentException("Sigma must be > 0", nameof(sigma));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        _mu = mu;
        _period = period;
        _invSigmaSqrt2 = 1.0 / (sigma * Math.Sqrt(2.0));
        _buffer = new RingBuffer(period);
        Name = $"Normdist({mu:F2},{sigma:F2},{period})";
        WarmupPeriod = period;
        _state = new State(0.5);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Normdist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="mu">Mean shift μ (default 0.0)</param>
    /// <param name="sigma">Scale σ &gt; 0 (default 1.0)</param>
    /// <param name="period">Lookback window (default 14)</param>
    public Normdist(ITValuePublisher source, double mu = 0.0, double sigma = 1.0, int period = 14)
        : this(mu, sigma, period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Error function approximation via Abramowitz &amp; Stegun 7.1.25 (3-term, max error ~2.5e-5).
    /// erf(x) ≈ 1 - (a1*t + a2*t² + a3*t³)*exp(-x²), t = 1/(1 + 0.47047*|x|)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Erf(double x)
    {
        const double p = 0.47047;
        const double a1 = 0.3480242;
        const double a2 = -0.0958798;
        const double a3 = 0.7478556;

        double ax = Math.Abs(x);
        double t = 1.0 / Math.FusedMultiplyAdd(p, ax, 1.0);
        double poly = Math.FusedMultiplyAdd(a3, t, a2);
        poly = Math.FusedMultiplyAdd(poly, t, a1);
        poly *= t;
        double val = 1.0 - poly * Math.Exp(-(ax * ax));
        return x >= 0.0 ? val : -val;
    }

    /// <summary>
    /// Normal Distribution CDF: Φ(x; μ, σ) = 0.5*(1 + erf((x-μ)/(σ*√2))).
    /// Returns 0.5 when σ ≤ 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NormalCdf(double x, double mu, double sigma)
    {
        if (sigma <= 0.0)
        {
            return 0.5;
        }

        double z = (x - mu) / (sigma * Math.Sqrt(2.0));
        return 0.5 * (1.0 + Erf(z));
    }

    /// <summary>
    /// Pure static CDF helper — identical to <see cref="NormalCdf"/> with an explicit name
    /// for downstream consumers and validation tests.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double StaticCdf(double x, double mu, double sigma) => NormalCdf(x, mu, sigma);

    /// <summary>
    /// Computes rolling mean and population standard deviation from a span of values.
    /// Returns (mean=0, stddev=0, count=0) when span is empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double mean, double stddev, int count) RollingStats(ReadOnlySpan<double> values)
    {
        double sum = 0.0;
        double sumSq = 0.0;
        int count = 0;

        for (int i = 0; i < values.Length; i++)
        {
            double v = values[i];
            if (double.IsFinite(v))
            {
                sum += v;
                sumSq = Math.FusedMultiplyAdd(v, v, sumSq);
                count++;
            }
        }

        if (count < 2)
        {
            return (0.0, 0.0, count);
        }

        double mean = sum / count;
        double variance = sumSq / count - mean * mean;
        double stddev = variance > 0.0 ? Math.Sqrt(variance) : 0.0;
        return (mean, stddev, count);
    }

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

            var (mean, stddev, count) = RollingStats(_buffer.GetSpan());

            if (count < 2)
            {
                result = 0.5;
            }
            else
            {
                // Z-score relative to rolling distribution
                double z = stddev > 0.0 ? (value - mean) / stddev : 0.0;

                // Apply user mu/sigma shift: z_final = (z - mu) / sigma → CDF input
                double zFinal = (z - _mu) * _invSigmaSqrt2;  // = (z-mu)/(sigma*sqrt(2))
                double erf = Erf(zFinal);
                result = 0.5 * (1.0 + erf);
            }

            _state = new State(result);
        }
        else
        {
            result = _state.LastValid;
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

    public static TSeries Batch(TSeries source, double mu = 0.0, double sigma = 1.0, int period = 14)
    {
        var indicator = new Normdist(mu, sigma, period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Normal Distribution CDF over a span of values.
    /// Uses a sliding window z-score normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        double mu = 0.0, double sigma = 1.0, int period = 14)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (sigma <= 0.0)
        {
            throw new ArgumentException("Sigma must be > 0", nameof(sigma));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        double invSigmaSqrt2 = 1.0 / (sigma * Math.Sqrt(2.0));
        double lastValid = 0.5;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                output[i] = lastValid;
                continue;
            }

            int start = Math.Max(0, i - period + 1);

            double sum = 0.0;
            double sumSq = 0.0;
            int count = 0;

            for (int j = start; j <= i; j++)
            {
                double v = source[j];
                if (double.IsFinite(v))
                {
                    sum += v;
                    sumSq = Math.FusedMultiplyAdd(v, v, sumSq);
                    count++;
                }
            }

            double result;

            if (count < 2)
            {
                result = 0.5;
            }
            else
            {
                double mean = sum / count;
                double variance = sumSq / count - mean * mean;
                double stddev = variance > 0.0 ? Math.Sqrt(variance) : 0.0;
                double z = stddev > 0.0 ? (val - mean) / stddev : 0.0;
                double zFinal = (z - mu) * invSigmaSqrt2;
                result = 0.5 * (1.0 + Erf(zFinal));
            }

            lastValid = result;
            output[i] = result;
        }
    }

    public static (TSeries Results, Normdist Indicator) Calculate(
        TSeries source, double mu = 0.0, double sigma = 1.0, int period = 14)
    {
        var indicator = new Normdist(mu, sigma, period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(0.5);
        _p_state = _state;
        Last = default;
    }
}
