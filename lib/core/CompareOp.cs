using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Contract for a comparison operator used by <see cref="ComparePredicateBase{TOp}"/>. Each
/// implementation is a zero-field readonly struct; the JIT specializes
/// <see cref="ComparePredicateBase{TOp}"/> per operator with no virtual dispatch, per the
/// strategy-primitives specification (Section 8.8).
/// </summary>
public interface ICompareOp
{
    /// <summary>Evaluates the comparison for two sanitized (finite) inputs.</summary>
#if NET5_0_OR_GREATER
    static abstract bool Eval(double a, double b, double eps);
#else
    bool Eval(double a, double b, double eps);
#endif
}

/// <summary>Strict greater-than: a &gt; b.</summary>
public readonly struct AboveOp : ICompareOp
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
    public static bool Eval(double a, double b, double eps) => a > b;
#else
    public bool Eval(double a, double b, double eps) => a > b;
#endif
}

/// <summary>Strict less-than: a &lt; b.</summary>
public readonly struct BelowOp : ICompareOp
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
    public static bool Eval(double a, double b, double eps) => a < b;
#else
    public bool Eval(double a, double b, double eps) => a < b;
#endif
}

/// <summary>Approximate equality within an epsilon: |a - b| &lt;= eps.</summary>
public readonly struct EqualOp : ICompareOp
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
    public static bool Eval(double a, double b, double eps) => Math.Abs(a - b) <= eps;
#else
    public bool Eval(double a, double b, double eps) => Math.Abs(a - b) <= eps;
#endif
}

/// <summary>
/// Shared implementation for the canonical crisp comparison predicates (<see cref="Above"/>,
/// <see cref="Below"/>, <see cref="Equal"/>). Reuses <see cref="BiInputIndicatorBase"/> with a
/// single-slot window (period = 1), the same pattern proven by <c>numerics/Add</c>,
/// <c>Sub</c>, <c>Mul</c>, and <c>Div</c>: the windowed mean degenerates to the current bar's
/// comparison result, so rollback and NaN-substitution come from the shared base with no extra
/// state. <see cref="ICompareOp.Eval"/> always returns a value convertible to a finite 0.0/1.0,
/// so the base's running-sum-poisoning hazard (documented on <c>BiInputIndicatorBase</c> and
/// <c>numerics/Div</c>) never applies here.
/// </summary>
/// <typeparam name="TOp">The comparison operator.</typeparam>
public abstract class ComparePredicateBase<TOp> : BiInputIndicatorBase
    where TOp : struct, ICompareOp
{
    private readonly double _epsilon;
    private double _scalarB;

    /// <summary>
    /// Creates a comparison predicate for direct pull-style updates.
    /// </summary>
    /// <param name="name">Predicate name for diagnostics</param>
    /// <param name="epsilon">Comparison tolerance passed to <typeparamref name="TOp"/></param>
    protected ComparePredicateBase(string name, double epsilon) : base(1, name)
    {
        _epsilon = epsilon;
        Last = new TValue(0, double.NaN);
    }

    /// <summary>
    /// Wires this predicate to two upstream publishers using the time-join rule
    /// (<see cref="BiInputIndicatorBase.Subscribe"/>).
    /// </summary>
    protected void SubscribeTwo(ITValuePublisher a, ITValuePublisher b) => Subscribe(a, b);

    /// <summary>
    /// Wires this predicate to one upstream publisher compared against a fixed scalar. Uses a
    /// dedicated path that never constructs a constant child publisher: the scalar is paired
    /// with each tick of <paramref name="a"/> directly.
    /// </summary>
    protected void SubscribeScalar(ITValuePublisher a, double b)
    {
        _scalarB = b;
        a.Pub += HandleScalarUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleScalarUpdate(object? sender, in TValueEventArgs e) =>
        Update(e.Value, new TValue(e.Value.Time, _scalarB), e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected sealed override double ComputeError(double actual, double predicted) =>
#if NET5_0_OR_GREATER
        TOp.Eval(actual, predicted, _epsilon) ? 1.0 : 0.0;
#else
        default(TOp).Eval(actual, predicted, _epsilon) ? 1.0 : 0.0;
#endif

    public override void Reset()
    {
        base.Reset();
        Last = new TValue(0, double.NaN);
    }
}
