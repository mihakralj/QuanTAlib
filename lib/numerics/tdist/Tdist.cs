// TDIST: Student's t-Distribution CDF
// Applies the one-tailed Student's t CDF F(t; ν) to a min-max normalized price series
// over a rolling lookback window.
// Pipeline: MinMax normalization → linear t-scaling to [-3,+3] → regularized incomplete beta.
// Reuses Betadist.IncompleteBeta internally — no gamma/CF reimplementation.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TDIST: Student's t-Distribution CDF
/// Computes the one-tailed CDF F(t; ν) via the regularized incomplete beta function,
/// applied to a min-max normalized price series mapped to t ∈ [-3, +3].
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range returns 0.5
/// - ν=1: Cauchy distribution (heavy tails); ν→∞: converges to Normal
/// - Reuses <see cref="Betadist.IncompleteBeta"/> — no special-function duplication
/// - NaN/Infinity inputs use last-valid-value substitution
/// </remarks>
[SkipLocalsInit]
public sealed class Tdist : AbstractBase
{
    private readonly int _period;
    private readonly int _nu;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Tdist indicator.
    /// </summary>
    /// <param name="nu">Degrees of freedom (integer ≥ 1, default 10)</param>
    /// <param name="period">Lookback window for min-max normalization (default 14)</param>
    public Tdist(int nu = 10, int period = 14)
    {
        if (nu < 1)
        {
            throw new ArgumentException("nu must be >= 1", nameof(nu));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        _nu = nu;
        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Tdist({nu},{period})";
        WarmupPeriod = period;
        _state = new State(0.5);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Tdist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="nu">Degrees of freedom (default 10)</param>
    /// <param name="period">Lookback window (default 14)</param>
    public Tdist(ITValuePublisher source, int nu = 10, int period = 14)
        : this(nu, period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// One-tailed Student's t CDF via regularized incomplete beta:
    ///   bx = ν / (ν + t²)
    ///   if t ≥ 0: CDF = 1 - 0.5 × I(bx, ν/2, 0.5)
    ///   if t &lt; 0: CDF = 0.5 × I(bx, ν/2, 0.5)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double TDistCdf(double t, int nu)
    {
        double nuD = nu;
        double t2 = t * t;
        double bx = nuD / Math.FusedMultiplyAdd(1.0, t2, nuD); // ν / (ν + t²)
        double ibeta = Betadist.IncompleteBeta(bx, nuD * 0.5, 0.5);
        return t >= 0.0 ? 1.0 - 0.5 * ibeta : 0.5 * ibeta;
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

            // Flat range → midpoint 0.5 → t=0 → CDF=0.5
            double xNorm = range > 0.0 ? (value - min) / range : 0.5;

            // Map [0,1] → [-3, +3]; covers ~99.7% of the std normal range
            double tVal = (xNorm - 0.5) * 6.0;

            result = TDistCdf(tVal, _nu);
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

    public static TSeries Batch(TSeries source, int nu = 10, int period = 14)
    {
        var indicator = new Tdist(nu, period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Student's t-Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        int nu = 10, int period = 14)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (nu < 1)
        {
            throw new ArgumentException("nu must be >= 1", nameof(nu));
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
            double tVal = (xNorm - 0.5) * 6.0;

            double result = TDistCdf(tVal, nu);
            lastValid = result;
            output[i] = result;
        }
    }

    /// <summary>
    /// Pure static T-CDF helper. Identical to <see cref="TDistCdf"/> but exposed
    /// with a more explicit name for downstream consumers and validation tests.
    /// </summary>
    public static double StaticCdf(double t, int nu) => TDistCdf(t, nu);

    public static (TSeries Results, Tdist Indicator) Calculate(
        TSeries source, int nu = 10, int period = 14)
    {
        var indicator = new Tdist(nu, period);
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
