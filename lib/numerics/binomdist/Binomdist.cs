using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BINOMDIST: Binomial Distribution CDF
/// Computes P(X ≤ k) for X ~ Binomial(n, p), where p is derived from the
/// min-max normalized position of the input price within its rolling window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always in [0, 1]
/// - Rolling window tracks min/max for normalization; flat range returns P(X≤k|p=0.5)
/// - p ≤ 0: returns 1.0 (all probability mass at X=0, P(X≤k)=1 for k≥0)
/// - p ≥ 1: returns 1.0 if k≥n, else 0.0 (all mass at X=n)
/// - Log-space computation via Lanczos log-gamma avoids factorial overflow for large n
/// </remarks>
[SkipLocalsInit]
public sealed class Binomdist : AbstractBase
{
    private readonly int _period;
    private readonly int _trials;
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
    /// Initializes a new Binomdist indicator.
    /// </summary>
    /// <param name="period">Lookback window for min-max normalization (default 50)</param>
    /// <param name="trials">Number of Bernoulli trials n (default 20)</param>
    /// <param name="threshold">Success threshold k — computes P(X ≤ k) (default 10)</param>
    public Binomdist(int period = 50, int trials = 20, int threshold = 10)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        if (trials < 1)
        {
            throw new ArgumentException("Trials must be >= 1", nameof(trials));
        }

        if (threshold < 0)
        {
            throw new ArgumentException("Threshold must be >= 0", nameof(threshold));
        }

        _period = period;
        _trials = trials;
        _threshold = threshold;
        _buffer = new RingBuffer(period);
        Name = $"Binomdist({period},{trials},{threshold})";
        WarmupPeriod = period;
        _state = new State(BinomCdf(0.5, trials, threshold));
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Binomdist indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback window (default 50)</param>
    /// <param name="trials">Number of Bernoulli trials n (default 20)</param>
    /// <param name="threshold">Success threshold k (default 10)</param>
    public Binomdist(ITValuePublisher source, int period = 50, int trials = 20, int threshold = 10)
        : this(period, trials, threshold)
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

        return 0.5 * Math.Log(2.0 * Math.PI)
             + (x + 0.5) * Math.Log(t)
             - t
             + Math.Log(ser);
    }

    /// <summary>
    /// Log of binomial coefficient: ln C(n, i) = lnGamma(n+1) - lnGamma(i+1) - lnGamma(n-i+1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double LnBinom(int n, int i)
        => LnGamma(n + 1.0) - LnGamma(i + 1.0) - LnGamma(n - i + 1.0);

    /// <summary>
    /// Binomial CDF P(X ≤ k) for X ~ Binomial(n, p) via log-space summation.
    /// Avoids factorial overflow for large n.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double BinomCdf(double p, int n, int k)
    {
        if (p <= 0.0)
        {
            return k >= 0 ? 1.0 : 0.0;
        }

        if (p >= 1.0)
        {
            return k >= n ? 1.0 : 0.0;
        }

        double lnP = Math.Log(p);
        double lnQ = Math.Log(1.0 - p);
        double cdf = 0.0;
        int kk = Math.Min(k, n);

        for (int i = 0; i <= kk; i++)
        {
            double lnTerm = Math.FusedMultiplyAdd(i, lnP, Math.FusedMultiplyAdd(n - i, lnQ, LnBinom(n, i)));
            cdf += Math.Exp(lnTerm);
        }

        return Math.Min(cdf, 1.0);
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

            // Flat range → neutral p=0.5
            double p = range > 0.0 ? (value - min) / range : 0.5;

            result = BinomCdf(p, _trials, _threshold);
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

    public static TSeries Batch(TSeries source, int period = 50, int trials = 20, int threshold = 10)
    {
        var indicator = new Binomdist(period, trials, threshold);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Binomial Distribution CDF over a span of values.
    /// Uses a sliding window min-max normalization identical to the streaming path.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        int period = 50, int trials = 20, int threshold = 10)
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

        if (trials < 1)
        {
            throw new ArgumentException("Trials must be >= 1", nameof(trials));
        }

        if (threshold < 0)
        {
            throw new ArgumentException("Threshold must be >= 0", nameof(threshold));
        }

        double lastValid = BinomCdf(0.5, trials, threshold);

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
            double p = range > 0.0 ? (val - min) / range : 0.5;

            double result = BinomCdf(p, trials, threshold);
            lastValid = result;
            output[i] = result;
        }
    }

    /// <summary>
    /// Exposes the Binomial CDF directly for testing and downstream consumers.
    /// </summary>
    public static double BinomialCdf(double p, int n, int k)
        => BinomCdf(p, n, k);

    public static (TSeries Results, Binomdist Indicator) Calculate(
        TSeries source, int period = 50, int trials = 20, int threshold = 10)
    {
        var indicator = new Binomdist(period, trials, threshold);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(BinomCdf(0.5, _trials, _threshold));
        _p_state = _state;
        Last = default;
    }
}
