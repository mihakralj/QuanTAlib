// BETADIST: Beta Distribution CDF
// Applies the regularized incomplete beta function I_x(alpha, beta) to a
// min-max normalized price series over a rolling lookback window.
// Pipeline: MinMax normalization → Lanczos log-gamma → Lentz continued fraction.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BETADIST: Beta Distribution CDF
/// Computes the regularized incomplete beta function I_x(alpha, beta) applied to
/// a min-max normalized price series over a rolling lookback window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range returns 0.5
/// - Shape parameters alpha and beta control the nonlinear mapping
/// - alpha=beta=1: identity (uniform distribution, no transform)
/// - alpha=beta=2: smooth S-curve compressing extremes, expanding midrange
/// - Lentz continued fraction with symmetry flip for numerical stability
/// - Lanczos log-gamma (g=7, 9 coefficients) for the beta function prefactor
/// </remarks>
[SkipLocalsInit]
public sealed class Betadist : AbstractBase
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _beta;
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
    /// Initializes a new Betadist indicator.
    /// </summary>
    /// <param name="period">Lookback window for min-max normalization (default 50)</param>
    /// <param name="alpha">First shape parameter of the Beta distribution (default 2.0)</param>
    /// <param name="beta">Second shape parameter of the Beta distribution (default 2.0)</param>
    public Betadist(int period = 50, double alpha = 2.0, double beta = 2.0)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        if (alpha <= 0.0)
        {
            throw new ArgumentException("Alpha must be > 0", nameof(alpha));
        }

        if (beta <= 0.0)
        {
            throw new ArgumentException("Beta must be > 0", nameof(beta));
        }

        _period = period;
        _alpha = alpha;
        _beta = beta;
        _buffer = new RingBuffer(period);
        Name = $"Betadist({period},{alpha:F1},{beta:F1})";
        WarmupPeriod = period;
        _state = new State(0.5);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Betadist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback window (default 50)</param>
    /// <param name="alpha">First shape parameter (default 2.0)</param>
    /// <param name="beta">Second shape parameter (default 2.0)</param>
    public Betadist(ITValuePublisher source, int period = 50, double alpha = 2.0, double beta = 2.0)
        : this(period, alpha, beta)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Lanczos approximation of ln(Gamma(z)) for z > 0.
    /// g=7, 9 coefficients — accurate to ~15 significant digits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double LnGamma(double z)
    {
        double x = z - 1.0;
        double t = x + 7.5; // g + 0.5 where g = 7
        double ser = LanczosCoeff[0];
        for (int k = 1; k <= 8; k++)
        {
            ser += LanczosCoeff[k] / (x + k);
        }

        return (0.5 * Math.Log(2.0 * Math.PI))
             + ((x + 0.5) * Math.Log(t))
             - t
             + Math.Log(ser);
    }

    /// <summary>
    /// Regularized incomplete beta function I_x(a,b) via Lentz continued fraction.
    /// Applies symmetry flip when x > (a+1)/(a+b+2) for guaranteed convergence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double RegularizedIncompleteBeta(double x, double a, double b)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        if (x >= 1.0)
        {
            return 1.0;
        }

        // Symmetry flip: when x > (a+1)/(a+b+2), evaluate at (1-x, b, a) for CF convergence.
        // Both the CF evaluation AND the ln-prefactor must use the flipped arguments.
        bool flipped = x > (a + 1.0) / (a + b + 2.0);

        double cfX, cfA, cfB;
        if (flipped)
        {
            cfX = 1.0 - x;
            cfA = b;
            cfB = a;
        }
        else
        {
            cfX = x;
            cfA = a;
            cfB = b;
        }

        double cf = BetaContinuedFraction(cfX, cfA, cfB);

        // ln-prefactor: cfX^cfA * (1-cfX)^cfB / (cfA * B(cfA,cfB))
        // B(a,b) = B(b,a) so the log-beta term is symmetric.
        double lnPrefactor = (cfA * Math.Log(cfX)) + (cfB * Math.Log(1.0 - cfX))
                           - Math.Log(cfA)
                           - (LnGamma(cfA) + LnGamma(cfB) - LnGamma(cfA + cfB));

        double result = Math.Exp(lnPrefactor) * cf;
        return flipped ? 1.0 - result : result;
    }

    /// <summary>
    /// Evaluates the continued fraction for the incomplete beta function
    /// using the modified Lentz algorithm. Max 200 iterations, eps=1e-14.
    /// </summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double BetaContinuedFraction(double x, double p, double q)
    {
        const double Eps = 1e-14;
        const double FpMin = 1e-300;
        const int MaxIter = 200;

        double qab = p + q;
        double qap = p + 1.0;
        double qam = p - 1.0;

        double c = 1.0;
        double d = 1.0 - (qab * x / qap);
        if (Math.Abs(d) < FpMin)
        {
            d = FpMin;
        }

        d = 1.0 / d;
        double h = d;

        for (int m = 1; m <= MaxIter; m++)
        {
            int m2 = 2 * m;

            // Even step: d_{2m}
            double aa = m * (q - m) * x / ((qam + m2) * (p + m2));
            d = 1.0 + (aa * d);
            if (Math.Abs(d) < FpMin)
            {
                d = FpMin;
            }

            c = 1.0 + (aa / c);
            if (Math.Abs(c) < FpMin)
            {
                c = FpMin;
            }

            d = 1.0 / d;
            h *= d * c;

            // Odd step: d_{2m+1}
            aa = -(p + m) * (qab + m) * x / ((p + m2) * (qap + m2));
            d = 1.0 + (aa * d);
            if (Math.Abs(d) < FpMin)
            {
                d = FpMin;
            }

            c = 1.0 + (aa / c);
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

        return h;
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

            // Flat range → neutral 0.5
            double x = range > 0.0 ? (value - min) / range : 0.5;

            // Clamp to open interval to avoid log(0) in the prefactor
            x = Math.Max(1e-14, Math.Min(1.0 - 1e-14, x));

            result = RegularizedIncompleteBeta(x, _alpha, _beta);
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

    public static TSeries Batch(TSeries source, int period = 50, double alpha = 2.0, double beta = 2.0)
    {
        var indicator = new Betadist(period, alpha, beta);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Beta Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        int period = 50, double alpha = 2.0, double beta = 2.0)
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

        if (alpha <= 0.0)
        {
            throw new ArgumentException("Alpha must be > 0", nameof(alpha));
        }

        if (beta <= 0.0)
        {
            throw new ArgumentException("Beta must be > 0", nameof(beta));
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
            double x = range > 0.0 ? (val - min) / range : 0.5;
            x = Math.Max(1e-14, Math.Min(1.0 - 1e-14, x));

            double result = RegularizedIncompleteBeta(x, alpha, beta);
            lastValid = result;
            output[i] = result;
        }
    }

    /// <summary>
    /// Exposes the regularized incomplete beta function I_x(a,b) directly.
    /// Useful for testing and for downstream consumers who have already normalized x.
    /// </summary>
    public static double IncompleteBeta(double x, double a, double b)
        => RegularizedIncompleteBeta(x, a, b);

    public static (TSeries Results, Betadist Indicator) Calculate(
        TSeries source, int period = 50, double alpha = 2.0, double beta = 2.0)
    {
        var indicator = new Betadist(period, alpha, beta);
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
