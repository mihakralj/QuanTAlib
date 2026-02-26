// FDIST: F-Distribution CDF
// Applies the Fisher-Snedecor CDF F(x; d1, d2) = I(d1*x/(d1*x+d2), d1/2, d2/2)
// to a min-max normalized price series over a rolling lookback window.
// Pipeline: MinMax normalization → scaling → regularized incomplete beta function.
// Reuses Betadist.IncompleteBeta internally — no gamma/CF reimplementation.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FDIST: F-Distribution (Fisher-Snedecor) CDF
/// Computes F(x; d1, d2) = I(d1·x/(d1·x+d2), d1/2, d2/2) applied to a
/// min-max normalized price series scaled to a positive real via a 10× factor.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range returns F(0.5·10; d1, d2)
/// - d1/d2 degrees of freedom control the shape: equal df → symmetric response,
///   d1 &gt; d2 → right-skewed, d1 &lt; d2 → left-skewed
/// - Reuses <see cref="Betadist.IncompleteBeta"/> — no special-function duplication
/// - NaN/Infinity inputs use last-valid-value substitution
/// </remarks>
[SkipLocalsInit]
public sealed class Fdist : AbstractBase
{
    private readonly int _period;
    private readonly int _d1;
    private readonly int _d2;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Fdist indicator.
    /// </summary>
    /// <param name="d1">Numerator degrees of freedom (integer ≥ 1, default 1)</param>
    /// <param name="d2">Denominator degrees of freedom (integer ≥ 1, default 1)</param>
    /// <param name="period">Lookback window for min-max normalization (default 14)</param>
    public Fdist(int d1 = 1, int d2 = 1, int period = 14)
    {
        if (d1 < 1)
        {
            throw new ArgumentException("d1 must be >= 1", nameof(d1));
        }

        if (d2 < 1)
        {
            throw new ArgumentException("d2 must be >= 1", nameof(d2));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        _d1 = d1;
        _d2 = d2;
        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Fdist({d1},{d2},{period})";
        WarmupPeriod = period;
        _state = new State(0.5);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Fdist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="d1">Numerator degrees of freedom (default 1)</param>
    /// <param name="d2">Denominator degrees of freedom (default 1)</param>
    /// <param name="period">Lookback window (default 14)</param>
    public Fdist(ITValuePublisher source, int d1 = 1, int d2 = 1, int period = 14)
        : this(d1, d2, period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// F-Distribution CDF: F(x; d1, d2) = I(d1·x/(d1·x+d2), d1/2, d2/2).
    /// Returns 0 for x ≤ 0, uses regularized incomplete beta for x > 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double FCdf(double x, int d1, int d2)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        double d1d = d1;
        double d2d = d2;
        double xBeta = d1d * x / Math.FusedMultiplyAdd(d1d, x, d2d);
        return Betadist.IncompleteBeta(xBeta, d1d * 0.5, d2d * 0.5);
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

            // Flat range → use midpoint 0.5; scale by 10 to spread F-CDF response across (0,∞)
            double xNorm = range > 0.0 ? (value - min) / range : 0.5;

            // Map [0,1] → [0,10] to place output in a useful part of the F-CDF response curve
            double xF = xNorm * 10.0;

            result = FCdf(xF, _d1, _d2);
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

    public static TSeries Batch(TSeries source, int d1 = 1, int d2 = 1, int period = 14)
    {
        var indicator = new Fdist(d1, d2, period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates F-Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        int d1 = 1, int d2 = 1, int period = 14)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (d1 < 1)
        {
            throw new ArgumentException("d1 must be >= 1", nameof(d1));
        }

        if (d2 < 1)
        {
            throw new ArgumentException("d2 must be >= 1", nameof(d2));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

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
            double xNorm = range > 0.0 ? (val - min) / range : 0.5;
            double xF = xNorm * 10.0;

            double result = FCdf(xF, d1, d2);
            lastValid = result;
            output[i] = result;
        }
    }

    /// <summary>
    /// Pure static F-CDF helper. Identical to <see cref="FCdf"/> but exposed
    /// with a more explicit name for downstream consumers and validation tests.
    /// </summary>
    public static double StaticCdf(double x, int d1, int d2) => FCdf(x, d1, d2);

    public static (TSeries Results, Fdist Indicator) Calculate(
        TSeries source, int d1 = 1, int d2 = 1, int period = 14)
    {
        var indicator = new Fdist(d1, d2, period);
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
