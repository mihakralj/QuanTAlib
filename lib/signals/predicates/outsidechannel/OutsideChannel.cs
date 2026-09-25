// OUTSIDECHANNEL: Channel Exclusion Predicate
// Crisp predicate: publishes 1.0 when value > upper or value < lower, 0.0 otherwise, NaN
// while cold.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// OUTSIDECHANNEL: Channel Exclusion Predicate
/// Tests whether a value is above the upper bound or below the lower bound of a channel.
/// </summary>
/// <remarks>
/// Key properties:
/// - Crisp output: exactly 1.0 (true) or 0.0 (false) while hot, NaN while cold
/// - The exact complement of <see cref="InsideChannel"/>: a value sitting exactly on a bound is
///   inside, never outside, so the two predicates never both fire on the same bar
/// - Built on <see cref="MultiInputBase"/> with 3 inputs (value, lower, upper)
/// - A fixed-scalar-bounds overload covers the common case of a fixed breakout zone
/// </remarks>
/// <seealso href="OutsideChannel.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class OutsideChannel : MultiInputBase
{
    private readonly double _scalarLower;
    private readonly double _scalarUpper;

    /// <summary>
    /// Creates an OutsideChannel predicate for direct pull-style updates.
    /// </summary>
    public OutsideChannel() : base(3, "OutsideChannel")
    {
    }

    /// <summary>
    /// Creates an OutsideChannel predicate chained to three upstream publishers (value, lower,
    /// upper) using the time-join rule.
    /// </summary>
    public OutsideChannel(ITValuePublisher value, ITValuePublisher lower, ITValuePublisher upper) : this()
    {
        Subscribe(value, lower, upper);
    }

    /// <summary>
    /// Creates an OutsideChannel predicate testing a stream against fixed scalar bounds.
    /// </summary>
    public OutsideChannel(ITValuePublisher value, double lower, double upper) : this()
    {
        _scalarLower = lower;
        _scalarUpper = upper;
        value.Pub += HandleScalarUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleScalarUpdate(object? sender, in TValueEventArgs e) =>
        Update(e.Value, new TValue(e.Value.Time, _scalarLower), new TValue(e.Value.Time, _scalarUpper), e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Compute(ReadOnlySpan<double> values)
    {
        double value = values[0], lower = values[1], upper = values[2];
        return value > upper || value < lower ? 1.0 : 0.0;
    }
}
