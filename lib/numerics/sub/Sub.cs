// SUB: Element-wise Subtraction
// Combines two live streams into their difference: result = a - b.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// SUB: Element-wise Subtraction
/// Computes result = a - b for two time-aligned input streams.
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
/// - Common use: spreads (a - b), MA gaps (fast - slow) feeding CrossOver
/// </remarks>
/// <seealso href="Sub.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Sub : BiInputIndicatorBase
{
    /// <summary>
    /// Creates a Sub node for direct pull-style updates.
    /// </summary>
    public Sub() : base(1, "Sub")
    {
    }

    /// <summary>
    /// Creates a Sub node chained to two upstream publishers using the time-join rule.
    /// </summary>
    /// <param name="a">Minuend publisher</param>
    /// <param name="b">Subtrahend publisher</param>
    public Sub(ITValuePublisher a, ITValuePublisher b) : this()
    {
        Subscribe(a, b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted) => actual - predicted;

    /// <summary>
    /// Computes a - b for two aligned series.
    /// </summary>
    public static TSeries Batch(TSeries a, TSeries b) => CalculateImpl(a, b, 1, BatchDelegate);

    /// <summary>
    /// Computes a - b element-wise using SIMD acceleration.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> output)
    {
        ValidateBatchInputs(a, b, output, 1);
        SimdExtensions.Subtract(a, b, output);
    }

    private static void BatchDelegate(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> output, int period) =>
        Batch(a, b, output);

    public static (TSeries Results, Sub Indicator) Calculate(TSeries a, TSeries b)
    {
        var indicator = new Sub();
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
