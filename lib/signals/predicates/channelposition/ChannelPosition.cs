// CHANNELPOSITION: Normalized Channel Position
// Graded predicate: publishes (value - lower) / (upper - lower), following an explicit
// OutOfRangePolicy when the result falls outside [0, 1], and NaN for a zero-width channel.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// CHANNELPOSITION: Normalized Channel Position
/// Returns the value's normalized position within a pair of channel bounds.
/// </summary>
/// <remarks>
/// Key properties:
/// - Graded output in <c>[0, 1]</c> while hot: 0.0 at the lower bound, 1.0 at the upper bound
/// - Zero-width channel (<c>|upper - lower| &lt;= DefaultEpsilon</c>): always publishes NaN and
///   reports not hot for that bar, regardless of policy — this is a division-by-zero guard, not
///   an out-of-range condition
/// - Value outside <c>[lower, upper]</c>: the raw ratio is outside <c>[0, 1]</c>. Governed by the
///   constructor's <see cref="OutOfRangePolicy"/>:
///   <see cref="OutOfRangePolicy.Cold"/> (default) publishes NaN and reports not hot for that
///   bar; <see cref="OutOfRangePolicy.Saturate"/> clamps the ratio into <c>[0, 1]</c>
/// - <see cref="AbstractBase.IsHot"/> (inherited via <see cref="MultiInputBase"/>) reflects both
///   "has a full set of inputs ever arrived" and "was the most recent computation valid" — unlike
///   most nodes, it can become false again on a specific bar without a <c>Reset()</c>
/// - Built on <see cref="MultiInputBase"/> with 3 inputs (value, lower, upper)
/// </remarks>
/// <seealso href="ChannelPosition.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class ChannelPosition : MultiInputBase
{
    private readonly OutOfRangePolicy _policy;
    private readonly double _scalarLower;
    private readonly double _scalarUpper;
    private bool _lastValid;

    /// <summary>
    /// True only when the most recent bar produced a valid, in-policy position. Combined with
    /// the base "has ever received a full input set" flag to form <see cref="IsHot"/>.
    /// </summary>
    public override bool IsHot => base.IsHot && _lastValid;

    /// <summary>
    /// Creates a ChannelPosition predicate for direct pull-style updates.
    /// </summary>
    /// <param name="policy">Behavior when the raw ratio falls outside [0, 1]; default is Cold</param>
    public ChannelPosition(OutOfRangePolicy policy = OutOfRangePolicy.Cold) : base(3, "ChannelPosition")
    {
        _policy = policy;
    }

    /// <summary>
    /// Creates a ChannelPosition predicate chained to three upstream publishers (value, lower,
    /// upper) using the time-join rule.
    /// </summary>
    public ChannelPosition(ITValuePublisher value, ITValuePublisher lower, ITValuePublisher upper, OutOfRangePolicy policy = OutOfRangePolicy.Cold)
        : this(policy)
    {
        Subscribe(value, lower, upper);
    }

    /// <summary>
    /// Creates a ChannelPosition predicate against fixed scalar bounds.
    /// </summary>
    public ChannelPosition(ITValuePublisher value, double lower, double upper, OutOfRangePolicy policy = OutOfRangePolicy.Cold)
        : this(policy)
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
        double width = upper - lower;

        if (Math.Abs(width) <= SignalConstants.DefaultEpsilon)
        {
            _lastValid = false;
            return double.NaN;
        }

        double ratio = (value - lower) / width;

        if (ratio is < 0.0 or > 1.0)
        {
            if (_policy == OutOfRangePolicy.Saturate)
            {
                _lastValid = true;
                return Math.Clamp(ratio, 0.0, 1.0);
            }

            _lastValid = false;
            return double.NaN;
        }

        _lastValid = true;
        return ratio;
    }
}
