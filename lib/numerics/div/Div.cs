// DIV: Element-wise Division
// Combines two live streams into their quotient: result = a / b.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// DIV: Element-wise Division
/// Computes result = a / b for two time-aligned input streams.
/// </summary>
/// <remarks>
/// Key properties:
/// - Stateless per bar: the result depends only on the current a and b
/// - NaN/Infinity in either input is replaced by that input's last valid value
/// - Built on <see cref="BiInputIndicatorBase"/> with a single-slot window (period = 1),
///   which supplies the isNew rollback and NaN-substitution machinery with no extra state.
///   IsHot becomes true after the first bar (a windowed base, not an immediate-hot transform)
/// - Event-chained construction applies the time-join rule: the result is only published
///   once both sources have reported for the same bar
///
/// Zero-denominator policy: publishes 0.0 when |b| is within <see cref="Epsilon"/> of zero,
/// rather than NaN or infinity. This is a deliberate, stateless choice: the ComputeError result
/// feeds a single-slot RingBuffer-backed running sum inherited from BiInputIndicatorBase, and a
/// non-finite ComputeError result would permanently poison that sum (the buffer content
/// self-corrects on the next bar, but the incrementally maintained sum does not). A "last valid
/// quotient" policy would need extra rollback-aware state that this base does not expose a hook
/// for. Callers that need last-valid-quotient semantics should guard the denominator upstream
/// (for example with a minimum-distance floor) before feeding it to Div.
/// </remarks>
/// <seealso href="Div.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Div : BiInputIndicatorBase
{
    /// <summary>
    /// Denominators with an absolute value at or below this threshold are treated as zero.
    /// </summary>
    public const double Epsilon = 1e-12;

    /// <summary>
    /// Creates a Div node for direct pull-style updates.
    /// </summary>
    public Div() : base(1, "Div")
    {
    }

    /// <summary>
    /// Creates a Div node chained to two upstream publishers using the time-join rule.
    /// </summary>
    /// <param name="a">Numerator publisher</param>
    /// <param name="b">Denominator publisher</param>
    public Div(ITValuePublisher a, ITValuePublisher b) : this()
    {
        Subscribe(a, b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted) =>
        Math.Abs(predicted) > Epsilon ? actual / predicted : 0.0;

    /// <summary>
    /// Computes a / b for two aligned series.
    /// </summary>
    public static TSeries Batch(TSeries a, TSeries b) => CalculateImpl(a, b, 1, BatchDelegate);

    /// <summary>
    /// Computes a / b element-wise using SIMD acceleration where the denominator is safe.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> output)
    {
        ValidateBatchInputs(a, b, output, 1);
        SimdExtensions.Divide(a, b, output, Epsilon);
    }

    private static void BatchDelegate(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> output, int period) =>
        Batch(a, b, output);

    public static (TSeries Results, Div Indicator) Calculate(TSeries a, TSeries b)
    {
        var indicator = new Div();
        TSeries results = indicator.Update(a, b);
        return (results, indicator);
    }

    /// <summary>
    /// Updates the node with two aligned series and returns the resulting series.
    /// </summary>
    public TSeries Update(TSeries a, TSeries b)
    {
        if (a.Count != b.Count)
        {
            throw new ArgumentException("Input series must have the same length", nameof(b));
        }

        var result = new TSeries(a.Count);
        for (int i = 0; i < a.Count; i++)
        {
            result.Add(Update(a[i], b[i], true), true);
        }
        return result;
    }
}
