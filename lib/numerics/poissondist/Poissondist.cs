// POISSONDIST: Poisson Distribution CDF
// Applies P(X ≤ k) = 1 - RegularizedLowerIncompleteGamma(k+1, λ) to a min-max
// normalized price series over a rolling lookback window.
// Pipeline: MinMax normalization → λ = xNorm * lambdaScale → Poisson-gamma identity → CDF.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// POISSONDIST: Poisson Distribution CDF
/// Computes P(X ≤ k; λ) — the Poisson cumulative distribution function — where λ is
/// derived from the min-max normalized price series over a rolling lookback window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range returns CDF at λ=lambdaScale*0.5
/// - Uses identity: P(X ≤ k) = 1 - P(k+1, λ) where P(a,x) is regularized lower incomplete gamma
/// - λ ≤ 0: returns 1.0 (degenerate; all probability mass at X=0)
/// - Series expansion for λ &lt; k+2; Lentz continued fraction otherwise
/// - Lanczos log-gamma (g=7, 9 coefficients) for numerical accuracy to 1e-15
/// - NaN/Infinity inputs use last-valid-value substitution
/// </remarks>
[SkipLocalsInit]
public sealed class Poissondist : AbstractBase
{
    private readonly int _period;
    private readonly double _lambdaScale;
    private readonly int _threshold;
    private readonly RingBuffer _buffer;

    // Lanczos g=7, 9 coefficients (Numerical Recipes 3rd Ed., Table 6.1)
    private static ReadOnlySpan<double> LanczosCoeff =>
    [
        0.99999999999980993,
        676.5203681218851,
        -1259.1392167224028,
        771.32342877765313,
        -176.61502916214059,
        12.507343278686905,
        -0.13857109526572012,
        9.9843695780195716e-6,
        1.5056327351493116e-7
    ];

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Poissondist indicator.
    /// </summary>
    /// <param name="lambda">Rate parameter λ &gt; 0 (default 1.0). Scales normalized price to event rate.</param>
    /// <param name="period">Lookback window for min-max normalization (default 14)</param>
    /// <param name="threshold">Integer threshold k ≥ 0; computes P(X ≤ k) (default 5)</param>
    public Poissondist(double lambda = 1.0, int period = 14, int threshold = 5)
    {
        if (lambda <= 0.0)
        {
            throw new ArgumentException("Lambda must be > 0", nameof(lambda));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        if (threshold < 0)
        {
            throw new ArgumentException("Threshold must be >= 0", nameof(threshold));
        }

        _lambdaScale = lambda;
        _threshold = threshold;
        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Poissondist({lambda:F2},{period},{threshold})";
        WarmupPeriod = period;
        _state = new State(PoissonCdf(_threshold, _lambdaScale * 0.5));
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Poissondist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="lambda">Rate parameter λ &gt; 0 (default 1.0)</param>
    /// <param name="period">Lookback window (default 14)</param>
    /// <param name="threshold">Integer threshold k ≥ 0 (default 5)</param>
    public Poissondist(ITValuePublisher source, double lambda = 1.0, int period = 14, int threshold = 5)
        : this(lambda, period, threshold)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Lanczos log-gamma approximation (g=7, 9 coefficients).
    /// Accurate to ~15 digits for z &gt; 0.5; uses reflection formula for z &lt; 0.5.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double LnGamma(double z)
    {
        if (z < 0.5)
        {
            return Math.Log(Math.PI / Math.Sin(Math.PI * z)) - LnGamma(1.0 - z);
        }

        z -= 1.0;
        ReadOnlySpan<double> c = LanczosCoeff;
        double x = c[0];
        for (int i = 1; i < 9; i++)
        {
            x += c[i] / (z + i);
        }

        double t = z + 7.5;
        return Math.FusedMultiplyAdd(z + 0.5, Math.Log(t), (0.5 * Math.Log(2.0 * Math.PI)) - t + Math.Log(x));
    }

    /// <summary>
    /// Series expansion for regularized lower incomplete gamma P(a, x).
    /// Converges for x &lt; a + 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GammaSeries(double a, double x, double lnGammaA)
    {
        const int MaxIter = 200;
        const double Eps = 1e-12;

        double ap = a;
        double sum = 1.0 / a;
        double del = 1.0 / a;

        for (int n = 0; n < MaxIter; n++)
        {
            ap += 1.0;
            del *= x / ap;
            sum += del;
            if (Math.Abs(del) < Math.Abs(sum) * Eps)
            {
                break;
            }
        }

        return sum * Math.Exp(-x + (a * Math.Log(x)) - lnGammaA);
    }

    /// <summary>
    /// Lentz continued fraction for regularized upper incomplete gamma Q(a, x) = 1 - P(a, x).
    /// Converges for x ≥ a + 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GammaCF(double a, double x, double lnGammaA)
    {
        const int MaxIter = 200;
        const double Eps = 1e-12;
        const double FpMin = 1e-300;

        double b = x + 1.0 - a;
        double c = 1.0 / FpMin;
        double d = 1.0 / b;
        double h = d;

        for (int i = 1; i <= MaxIter; i++)
        {
            double an = -(double)i * (i - a);
            b += 2.0;
            d = Math.FusedMultiplyAdd(an, d, b);
            if (Math.Abs(d) < FpMin)
            {
                d = FpMin;
            }

            c = b + (an / c);
            if (Math.Abs(c) < FpMin)
            {
                c = FpMin;
            }

            d = 1.0 / d;
            double del = d * c;
            h *= del;
            if (Math.Abs(del - 1.0) < Eps)
            {
                break;
            }
        }

        return Math.Exp(-x + (a * Math.Log(x)) - lnGammaA) * h;
    }

    /// <summary>
    /// Regularized lower incomplete gamma function P(a, x) = γ(a,x)/Γ(a).
    /// Uses series for x &lt; a+1; complement of CF for x ≥ a+1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double RegularizedIncompleteGamma(double a, double x, double lnGammaA)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        if (x < a + 1.0)
        {
            return GammaSeries(a, x, lnGammaA);
        }

        return 1.0 - GammaCF(a, x, lnGammaA);
    }

    /// <summary>
    /// Poisson CDF: P(X ≤ k; λ) = 1 - P(k+1, λ) using the gamma-Poisson identity.
    /// Returns 1.0 for λ ≤ 0 (degenerate: all mass at X=0).
    /// Returns 0.0 for k &lt; 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PoissonCdf(int k, double lambda)
    {
        if (k < 0)
        {
            return 0.0;
        }

        if (lambda <= 0.0)
        {
            return 1.0;
        }

        double a = k + 1.0;
        double lnGammaA = LnGamma(a);
        return 1.0 - RegularizedIncompleteGamma(a, lambda, lnGammaA);
    }

    /// <summary>
    /// Exposes the Poisson CDF directly for testing and downstream consumers.
    /// Identical to <see cref="PoissonCdf"/>.
    /// </summary>
    public static double StaticCdf(int k, double lambda) => PoissonCdf(k, lambda);

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

            // Flat range → neutral x=0.5; map [0,1] → [0, lambdaScale]
            double xNorm = range > 0.0 ? (value - min) / range : 0.5;
            double lambda = xNorm * _lambdaScale;

            result = PoissonCdf(_threshold, lambda);
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

    public static TSeries Batch(TSeries source, double lambda = 1.0, int period = 14, int threshold = 5)
    {
        var indicator = new Poissondist(lambda, period, threshold);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Poisson Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        double lambda = 1.0, int period = 14, int threshold = 5)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (lambda <= 0.0)
        {
            throw new ArgumentException("Lambda must be > 0", nameof(lambda));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        if (threshold < 0)
        {
            throw new ArgumentException("Threshold must be >= 0", nameof(threshold));
        }

        double lastValid = PoissonCdf(threshold, lambda * 0.5);

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
            double lam = xNorm * lambda;

            double result = PoissonCdf(threshold, lam);
            lastValid = result;
            output[i] = result;
        }
    }

    public static (TSeries Results, Poissondist Indicator) Calculate(
        TSeries source, double lambda = 1.0, int period = 14, int threshold = 5)
    {
        var indicator = new Poissondist(lambda, period, threshold);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(PoissonCdf(_threshold, _lambdaScale * 0.5));
        _p_state = _state;
        Last = default;
    }
}
