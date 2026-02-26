// EXPDIST: Exponential Distribution CDF
// Applies the exponential CDF F(x; λ) = 1 - exp(-λx) to a min-max normalized
// price series over a rolling lookback window.
// Pipeline: MinMax normalization → closed-form CDF evaluation (single exp() call).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EXPDIST: Exponential Distribution CDF
/// Computes the exponential CDF F(x; λ) = 1 - exp(-λx) applied to a min-max
/// normalized price series over a rolling lookback window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range returns F(0.5; λ)
/// - λ (lambda) controls curvature: higher λ compresses the CDF toward 1.0 faster
/// - λ = 1: gentle curve, F(0.5) ≈ 0.39; λ = 3 (default): F(0.5) ≈ 0.78
/// - CDF evaluation is O(1): a single exp() — no special functions required
/// - NaN/Infinity inputs use last-valid-value substitution
/// </remarks>
[SkipLocalsInit]
public sealed class Expdist : AbstractBase
{
    private readonly int _period;
    private readonly double _lambda;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Expdist indicator.
    /// </summary>
    /// <param name="period">Lookback window for min-max normalization (default 50)</param>
    /// <param name="lambda">Rate parameter λ &gt; 0 (default 3.0)</param>
    public Expdist(int period = 50, double lambda = 3.0)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        if (lambda <= 0.0)
        {
            throw new ArgumentException("Lambda must be > 0", nameof(lambda));
        }

        _period = period;
        _lambda = lambda;
        _buffer = new RingBuffer(period);
        Name = $"Expdist({period},{lambda:F2})";
        WarmupPeriod = period;
        _state = new State(0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Expdist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback window (default 50)</param>
    /// <param name="lambda">Rate parameter λ &gt; 0 (default 3.0)</param>
    public Expdist(ITValuePublisher source, int period = 50, double lambda = 3.0)
        : this(period, lambda)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Exponential CDF: F(x; λ) = 1 - exp(-λx) for x > 0, else 0.
    /// Closed-form; requires only a single exp() call.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ExpCdf(double x, double lambda)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        return 1.0 - Math.Exp(-lambda * x);
    }

    /// <summary>
    /// Exponential PDF: f(x; λ) = λ * exp(-λx) for x >= 0, else 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ExpPdf(double x, double lambda)
    {
        if (x < 0.0)
        {
            return 0.0;
        }

        return lambda * Math.Exp(Math.FusedMultiplyAdd(-lambda, x, 0.0));
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

            result = ExpCdf(x, _lambda);
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

    public static TSeries Batch(TSeries source, int period = 50, double lambda = 3.0)
    {
        var indicator = new Expdist(period, lambda);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Exponential Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        int period = 50, double lambda = 3.0)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        if (lambda <= 0.0)
        {
            throw new ArgumentException("Lambda must be > 0", nameof(lambda));
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

            double result = ExpCdf(x, lambda);
            lastValid = result;
            output[i] = result;
        }
    }

    public static (TSeries Results, Expdist Indicator) Calculate(
        TSeries source, int period = 50, double lambda = 3.0)
    {
        var indicator = new Expdist(period, lambda);
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
