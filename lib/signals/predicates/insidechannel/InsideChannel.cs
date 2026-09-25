// INSIDECHANNEL: Channel Membership Predicate
// Crisp predicate: publishes 1.0 when lower <= value <= upper, 0.0 otherwise, NaN while cold.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// INSIDECHANNEL: Channel Membership Predicate
/// Tests whether a value is inside (inclusive) a pair of channel bounds.
/// </summary>
/// <remarks>
/// Key properties:
/// - Crisp output: exactly 1.0 (true) or 0.0 (false) while hot, NaN while cold
/// - Bounds are inclusive: <c>lower &lt;= value &lt;= upper</c>
/// - Works with any three time-aligned streams, including the Upper/Lower outputs of any
///   existing channel indicator (Bollinger, Keltner, Donchian, ...)
/// - A fixed-scalar-bounds overload covers the common case of a fixed oscillator zone
///   (e.g. RSI between 30 and 70) without wiring two extra constant streams
/// - Built on <see cref="MultiInputBase"/> with 3 inputs (value, lower, upper)
/// </remarks>
/// <seealso href="InsideChannel.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class InsideChannel : MultiInputBase
{
    private readonly double _scalarLower;
    private readonly double _scalarUpper;

    /// <summary>
    /// Creates an InsideChannel predicate for direct pull-style updates.
    /// </summary>
    public InsideChannel() : base(3, "InsideChannel")
    {
    }

    /// <summary>
    /// Creates an InsideChannel predicate chained to three upstream publishers (value, lower,
    /// upper) using the time-join rule.
    /// </summary>
    public InsideChannel(ITValuePublisher value, ITValuePublisher lower, ITValuePublisher upper) : this()
    {
        Subscribe(value, lower, upper);
    }

    /// <summary>
    /// Creates an InsideChannel predicate testing a stream against fixed scalar bounds.
    /// </summary>
    public InsideChannel(ITValuePublisher value, double lower, double upper) : this()
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
        return value >= lower && value <= upper ? 1.0 : 0.0;
    }
}
