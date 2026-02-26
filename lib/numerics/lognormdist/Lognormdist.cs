// LOGNORMDIST: Log-Normal Distribution CDF
// Applies F(x; μ, σ) = Φ((ln(x) - μ) / σ) to a min-max normalized price series
// over a rolling lookback window.
// Pipeline: MinMax normalization → floor at 1e-10 → log-standardization → normal CDF.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LOGNORMDIST: Log-Normal Distribution CDF
/// Computes F(x; μ, σ) = Φ((ln(x) - μ) / σ) applied to a min-max normalized
/// price series over a rolling lookback window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range uses x=0.5
/// - min-max x is floored at 1e-10 before log to prevent ln(0)
/// - μ shifts the inflection point of the S-curve along the logarithmic axis
/// - σ controls steepness: small σ → sharp transition, large σ → gradual
/// - Normal CDF: Abramowitz &amp; Stegun 7.1.26 (5-term), max error ~1.5e-7
/// - NaN/Infinity inputs use last-valid-value substitution
/// </remarks>
[SkipLocalsInit]
public sealed class Lognormdist : AbstractBase
{
    private readonly int _period;
    private readonly double _mu;
    private readonly double _invSigma;        // precomputed: 1 / sigma
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Lognormdist indicator.
    /// </summary>
    /// <param name="mu">Log-mean μ — mean of ln(X) (default 0.0)</param>
    /// <param name="sigma">Log-std σ &gt; 0 — std dev of ln(X) (default 1.0)</param>
    /// <param name="period">Lookback window for min-max normalization (default 14)</param>
    public Lognormdist(double mu = 0.0, double sigma = 1.0, int period = 14)
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
        _invSigma = 1.0 / sigma;
        _buffer = new RingBuffer(period);
        Name = $"Lognormdist({mu:F2},{sigma:F2},{period})";
        WarmupPeriod = period;
        _state = new State(0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Lognormdist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="mu">Log-mean μ (default 0.0)</param>
    /// <param name="sigma">Log-std σ &gt; 0 (default 1.0)</param>
    /// <param name="period">Lookback window (default 14)</param>
    public Lognormdist(ITValuePublisher source, double mu = 0.0, double sigma = 1.0, int period = 14)
        : this(mu, sigma, period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Standard normal CDF Φ(z) via Abramowitz &amp; Stegun 7.1.26 (5-term, max error ~1.5e-7).
    /// Φ(z) = 1 - φ(|z|) * (b1*t + b2*t² + b3*t³ + b4*t⁴ + b5*t⁵), t = 1/(1 + 0.2316419|z|)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double NormalCdf(double z)
    {
        const double P  = 0.2316419;
        const double B1 =  0.319381530;
        const double B2 = -0.356563782;
        const double B3 =  1.781477937;
        const double B4 = -1.821255978;
        const double B5 =  1.330274429;

        double az = Math.Abs(z);
        double t = 1.0 / Math.FusedMultiplyAdd(P, az, 1.0);
        double phi = Math.Exp(-0.5 * az * az) * (1.0 / Math.Sqrt(2.0 * Math.PI));
        double poly = ((((Math.FusedMultiplyAdd(B5, t, B4) * t) + B3) * t + B2) * t + B1) * t;
        double cdf = 1.0 - phi * poly;
        return z >= 0.0 ? cdf : 1.0 - cdf;
    }

    /// <summary>
    /// Log-Normal CDF: F(x; μ, σ) = Φ((ln(x) - μ) / σ) for x &gt; 0, else 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double LogNormalCdf(double x, double mu, double sigma)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        double z = (Math.Log(x) - mu) / sigma;
        return NormalCdf(z);
    }

    /// <summary>
    /// Pure static CDF helper — identical to <see cref="LogNormalCdf"/> with an explicit name
    /// for downstream consumers and validation tests.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double StaticCdf(double x, double mu, double sigma) => LogNormalCdf(x, mu, sigma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double min, double max) FindMinMax(ReadOnlySpan<double> values)
    {
        if (values.Length == 0)
        {
            return (double.MaxValue, double.MinValue);
        }

        double min = values[0];
        double max = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            double v = values[i];
            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }

        return (min, max);
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

            var (min, max) = FindMinMax(_buffer.GetSpan());
            double range = max - min;

            // Flat range → use midpoint 0.5 to avoid degenerate output
            double x = range > 0.0 ? (value - min) / range : 0.5;

            // Floor to prevent ln(0); safeX in (0, 1]
            double safeX = x < 1e-10 ? 1e-10 : x;

            // Log-standardize: z = (ln(safeX) - mu) / sigma
            double z = Math.FusedMultiplyAdd(Math.Log(safeX), _invSigma, -_mu * _invSigma);

            result = NormalCdf(z);
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
        var indicator = new Lognormdist(mu, sigma, period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Log-Normal Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
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

        double invSigma = 1.0 / sigma;
        double lastValid = 0.0;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                output[i] = lastValid;
                continue;
            }

            int start = Math.Max(0, i - period + 1);

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;

            for (int j = start; j <= i; j++)
            {
                double v = source[j];
                if (double.IsFinite(v))
                {
                    if (v < min)
                    {
                        min = v;
                    }

                    if (v > max)
                    {
                        max = v;
                    }
                }
            }

            if (!double.IsFinite(min) || !double.IsFinite(max))
            {
                output[i] = lastValid;
                continue;
            }

            double range = max - min;
            double x = range > 0.0 ? (val - min) / range : 0.5;
            double safeX = x < 1e-10 ? 1e-10 : x;
            double z = Math.FusedMultiplyAdd(Math.Log(safeX), invSigma, -mu * invSigma);

            double result = NormalCdf(z);
            lastValid = result;
            output[i] = result;
        }
    }

    public static (TSeries Results, Lognormdist Indicator) Calculate(
        TSeries source, double mu = 0.0, double sigma = 1.0, int period = 14)
    {
        var indicator = new Lognormdist(mu, sigma, period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(0.0);
        _p_state = _state;
        Last = default;
    }
}
