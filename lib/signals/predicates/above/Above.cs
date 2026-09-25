// ABOVE: Greater-Than Predicate
// Crisp predicate: publishes 1.0 when a > b, 0.0 otherwise, NaN while cold.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ABOVE: Greater-Than Predicate
/// Tests whether the left value is strictly greater than the right value or a fixed scalar.
/// </summary>
/// <remarks>
/// Key properties:
/// - Crisp output: exactly 1.0 (true) or 0.0 (false) while hot, NaN while cold
/// - Built on <see cref="ComparePredicateBase{TOp}"/> (a thin sealed class over
///   <see cref="BiInputIndicatorBase"/> at period = 1): rollback and NaN-substitution for the
///   two inputs come from the shared base with no extra state
/// - The scalar overload never constructs a constant child publisher
/// - Feeds triggers such as <c>CrossOver</c> and combinators such as <c>AllOf</c>
/// </remarks>
/// <seealso href="Above.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Above : ComparePredicateBase<AboveOp>
{
    /// <summary>
    /// Creates an Above predicate for direct pull-style updates.
    /// </summary>
    public Above() : base("Above", SignalConstants.DefaultEpsilon)
    {
    }

    /// <summary>
    /// Creates an Above predicate chained to two upstream publishers using the time-join rule.
    /// </summary>
    public Above(ITValuePublisher a, ITValuePublisher b) : this()
    {
        SubscribeTwo(a, b);
    }

    /// <summary>
    /// Creates an Above predicate testing a stream against a fixed scalar threshold.
    /// </summary>
    public Above(ITValuePublisher a, double threshold) : this()
    {
        SubscribeScalar(a, threshold);
    }
}
