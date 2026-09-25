// BELOW: Less-Than Predicate
// Crisp predicate: publishes 1.0 when a < b, 0.0 otherwise, NaN while cold.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// BELOW: Less-Than Predicate
/// Tests whether the left value is strictly less than the right value or a fixed scalar.
/// </summary>
/// <remarks>
/// Key properties:
/// - Crisp output: exactly 1.0 (true) or 0.0 (false) while hot, NaN while cold
/// - Built on <see cref="ComparePredicateBase{TOp}"/> (a thin sealed class over
///   <see cref="BiInputIndicatorBase"/> at period = 1): rollback and NaN-substitution for the
///   two inputs come from the shared base with no extra state
/// - The scalar overload never constructs a constant child publisher
/// - Feeds triggers such as <c>CrossUnder</c> and combinators such as <c>AllOf</c>
/// </remarks>
/// <seealso href="Below.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Below : ComparePredicateBase<BelowOp>
{
    /// <summary>
    /// Creates a Below predicate for direct pull-style updates.
    /// </summary>
    public Below() : base("Below", SignalConstants.DefaultEpsilon)
    {
    }

    /// <summary>
    /// Creates a Below predicate chained to two upstream publishers using the time-join rule.
    /// </summary>
    public Below(ITValuePublisher a, ITValuePublisher b) : this()
    {
        SubscribeTwo(a, b);
    }

    /// <summary>
    /// Creates a Below predicate testing a stream against a fixed scalar threshold.
    /// </summary>
    public Below(ITValuePublisher a, double threshold) : this()
    {
        SubscribeScalar(a, threshold);
    }
}
