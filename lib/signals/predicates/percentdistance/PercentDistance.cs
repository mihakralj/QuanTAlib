// PERCENTDISTANCE: Relative Distance
// Raw ratio (a - b) / b. A documented exception to the [0, 1] / crisp value contract: this
// feeds Above/Below rather than being a predicate output itself.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// PERCENTDISTANCE: Relative Distance
/// Computes the signed relative distance of a from b: (a - b) / b.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output is an unbounded signed ratio, not a crisp 0/1 or graded [0, 1] value — a documented
///   exception to the Section 8.1 range rule, exactly as the strategy-primitives specification
///   calls out (Section 9.1). It exists to feed <see cref="Above"/> or <see cref="Below"/>
///   (for example <c>Above(new PercentDistance(price, sma), 0.02)</c> to test "more than 2% above
///   its moving average"), not to be read as a predicate result on its own.
/// - Zero-denominator policy matches <c>numerics/Div</c>: publishes 0.0 (not NaN) when
///   <c>|b| &lt;= Div.Epsilon</c>, for the same reason — a non-finite <c>ComputeError</c> result
///   would permanently poison the shared <see cref="BiInputIndicatorBase"/> running sum
/// - Built on <see cref="BiInputIndicatorBase"/> with a single-slot window (period = 1), the same
///   reuse pattern as <c>numerics/Add</c>/<c>Sub</c>/<c>Mul</c>/<c>Div</c>
/// </remarks>
/// <seealso href="PercentDistance.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class PercentDistance : BiInputIndicatorBase
{
    /// <summary>
    /// Creates a PercentDistance node for direct pull-style updates.
    /// </summary>
    public PercentDistance() : base(1, "PercentDistance")
    {
        Last = new TValue(0, double.NaN);
    }

    /// <summary>
    /// Creates a PercentDistance node chained to two upstream publishers using the time-join rule.
    /// </summary>
    public PercentDistance(ITValuePublisher a, ITValuePublisher b) : this()
    {
        Subscribe(a, b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted) =>
        Math.Abs(predicted) > Div.Epsilon ? (actual - predicted) / predicted : 0.0;

    public override void Reset()
    {
        base.Reset();
        Last = new TValue(0, double.NaN);
    }
}
