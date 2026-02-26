// WEIBULLDIST: Weibull Distribution CDF
// Applies F(x; k, λ) = 1 - exp(-(x/λ)^k) to a min-max normalized price series
// over a rolling lookback window.
// Pipeline: MinMax normalization → closed-form CDF evaluation (one pow + one exp).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// WEIBULLDIST: Weibull Distribution CDF
/// Computes F(x; k, λ) = 1 - exp(-(x/λ)^k) applied to a min-max normalized
/// price series over a rolling lookback window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range returns F(0.5; k, λ)
/// - k (shape) controls CDF curvature: k&lt;1 concave, k=1 exponential, k=2 Rayleigh, k&gt;3 S-curve
/// - λ (scale) controls rise speed: larger λ → slower rise, smaller λ → faster saturation
/// - CDF at x=λ equals 1 - e^(-1) ≈ 0.6321 for any k (characteristic life property)
/// - Two operations: one Math.Pow + one Math.Exp — no special functions required
/// - NaN/Infinity inputs use last-valid-value substitution
/// </remarks>
[SkipLocalsInit]
public sealed class Weibulldist : AbstractBase
{
    private readonly int _period;
    private readonly double _k;
    private readonly double _invLambda;  // precomputed: 1 / lambda
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Weibulldist indicator.
    /// </summary>
    /// <param name="k">Shape parameter k &gt; 0 (default 1.5)</param>
    /// <param name="lambda">Scale parameter λ &gt; 0 (default 1.0)</param>
    /// <param name="period">Lookback window for min-max normalization (default 14)</param>
    public Weibulldist(double k = 1.5, double lambda = 1.0, int period = 14)
    {
        if (k <= 0.0)
        {
            throw new ArgumentException("Shape k must be > 0", nameof(k));
        }

        if (lambda <= 0.0)
        {
            throw new ArgumentException("Scale lambda must be > 0", nameof(lambda));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        _k = k;
        _invLambda = 1.0 / lambda;
        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Weibulldist({k:F2},{lambda:F2},{period})";
        WarmupPeriod = period;
        _state = new State(0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Weibulldist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="k">Shape parameter k &gt; 0 (default 1.5)</param>
    /// <param name="lambda">Scale parameter λ &gt; 0 (default 1.0)</param>
    /// <param name="period">Lookback window (default 14)</param>
    public Weibulldist(ITValuePublisher source, double k = 1.5, double lambda = 1.0, int period = 14)
        : this(k, lambda, period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Weibull CDF: F(x; k, λ) = 1 - exp(-(x/λ)^k) for x &gt; 0, else 0.
    /// Closed-form; requires one Math.Pow + one Math.Exp call.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double StaticCdf(double x, double k, double lambda)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        return 1.0 - Math.Exp(-Math.Pow(x / lambda, k));
    }

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

            // x ∈ [0,1]; apply Weibull CDF directly (λ scales within [0,1] domain)
            result = 1.0 - Math.Exp(-Math.Pow(x * _invLambda, _k));
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

    public static TSeries Batch(TSeries source, double k = 1.5, double lambda = 1.0, int period = 14)
    {
        var indicator = new Weibulldist(k, lambda, period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Weibull Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        double k = 1.5, double lambda = 1.0, int period = 14)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (k <= 0.0)
        {
            throw new ArgumentException("Shape k must be > 0", nameof(k));
        }

        if (lambda <= 0.0)
        {
            throw new ArgumentException("Scale lambda must be > 0", nameof(lambda));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        double invLambda = 1.0 / lambda;
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

            double result = 1.0 - Math.Exp(-Math.Pow(x * invLambda, k));
            lastValid = result;
            output[i] = result;
        }
    }

    public static (TSeries Results, Weibulldist Indicator) Calculate(
        TSeries source, double k = 1.5, double lambda = 1.0, int period = 14)
    {
        var indicator = new Weibulldist(k, lambda, period);
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
