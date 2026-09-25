namespace QuanTAlib;

/// <summary>
/// Named constants shared by the <c>signals/</c>, <c>regimes/</c>, <c>stops/</c>, and
/// <c>sizing/</c> layers, per the value-semantics contract in the strategy-primitives
/// specification (<c>lib/signals/StrategyPrimitives.Spec.md</c>, Section 8.1).
/// </summary>
public static class SignalConstants
{
    /// <summary>
    /// Default tolerance for the <c>Equal</c> predicate and other value-equality comparisons in
    /// this layer. Matches the <c>1e-10</c> convention already used by per-class epsilon
    /// constants throughout the library (see, for example, <c>momentum/Rs</c>,
    /// <c>statistics/Correl</c>). Deliberately not <see cref="double.Epsilon"/>, which is a
    /// machine-level spacing value and is unsuitable as a financial comparison tolerance.
    /// </summary>
    public const double DefaultEpsilon = 1e-10;

    /// <summary>
    /// Default threshold at which a graded value in <c>[0, 1]</c> counts as true for nodes that
    /// need a boolean from a continuous input (triggers, <c>Latch</c>, <c>KOfN</c>, guards). For
    /// crisp (already 0.0/1.0) inputs this threshold never matters.
    /// </summary>
    public const double TruthThreshold = 0.5;
}

/// <summary>
/// Policy for a graded predicate (or any node documented as following this policy) when its
/// input falls outside the value's expected range, most commonly outside <c>[0, 1]</c>.
/// </summary>
public enum OutOfRangePolicy
{
    /// <summary>
    /// Publish <see cref="double.NaN"/> and report the node as not hot for that bar, rather than
    /// silently clamping. This is the default: a node that expects <c>[0, 1]</c> input must
    /// reject or explicitly handle values outside that range.
    /// </summary>
    Cold = 0,

    /// <summary>
    /// Clamp the value into range. An explicit, documented choice made by the caller; never the
    /// implicit default.
    /// </summary>
    Saturate = 1,
}
