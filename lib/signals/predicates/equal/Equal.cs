// EQUAL: Approximate Equality Predicate
// Crisp predicate: publishes 1.0 when |a - b| <= epsilon, 0.0 otherwise, NaN while cold.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// EQUAL: Approximate Equality Predicate
/// Tests whether two values (or a value and a fixed scalar) are equal within an epsilon.
/// </summary>
/// <remarks>
/// Key properties:
/// - Crisp output: exactly 1.0 (true) or 0.0 (false) while hot, NaN while cold
/// - Epsilon defaults to <see cref="SignalConstants.DefaultEpsilon"/> (1e-10), never
///   <see cref="double.Epsilon"/>, which is a machine-level spacing value unsuitable as a
///   financial comparison tolerance
/// - Built on <see cref="ComparePredicateBase{TOp}"/> (a thin sealed class over
///   <see cref="BiInputIndicatorBase"/> at period = 1): rollback and NaN-substitution for the
///   two inputs come from the shared base with no extra state
/// - This is a value predicate, not C# object identity or structural equality
/// </remarks>
/// <seealso href="Equal.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Equal : ComparePredicateBase<EqualOp>
{
    /// <summary>
    /// Creates an Equal predicate for direct pull-style updates.
    /// </summary>
    /// <param name="epsilon">Comparison tolerance; defaults to the library-wide default</param>
    public Equal(double epsilon = SignalConstants.DefaultEpsilon) : base("Equal", epsilon)
    {
        if (epsilon < 0.0 || !double.IsFinite(epsilon))
        {
            throw new ArgumentException("Epsilon must be a finite, non-negative number", nameof(epsilon));
        }
    }

    /// <summary>
    /// Creates an Equal predicate chained to two upstream publishers using the time-join rule.
    /// </summary>
    public Equal(ITValuePublisher a, ITValuePublisher b, double epsilon = SignalConstants.DefaultEpsilon) : this(epsilon)
    {
        SubscribeTwo(a, b);
    }

    /// <summary>
    /// Creates an Equal predicate testing a stream against a fixed scalar.
    /// </summary>
    public Equal(ITValuePublisher a, double scalar, double epsilon = SignalConstants.DefaultEpsilon) : this(epsilon)
    {
        SubscribeScalar(a, scalar);
    }
}
