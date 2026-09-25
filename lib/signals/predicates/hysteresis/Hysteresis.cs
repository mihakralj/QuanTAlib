// HYSTERESIS: Schmitt-Trigger Predicate
// Crisp latched predicate: removes boundary flicker around a single threshold by requiring
// the input to clear an "enter" level before latching true, and a lower "exit" level before
// latching back to false.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HYSTERESIS: Schmitt-Trigger Predicate
/// Latches true once the input reaches <c>enter</c>; stays true until the input falls to
/// <c>exit</c> or below, then latches false until <c>enter</c> is reached again.
/// </summary>
/// <remarks>
/// Key properties:
/// - Crisp output: exactly 1.0 (true) or 0.0 (false) while hot, NaN before the first value
/// - Requires <c>enter &gt;= exit</c>, defining a dead band between the two thresholds where
///   the previous latched state persists; this is the "upward latch" polarity referenced by the
///   strategy-primitives specification's note on <c>Above</c>/<c>Below</c> (Section 9.1): it
///   removes the repeated flip-flopping a plain <c>Above(threshold)</c> produces when a noisy
///   value hovers exactly at the boundary
/// - <c>enter == exit</c> degenerates to a plain threshold test with no dead band
/// - NaN/Infinity inputs hold the last latched state rather than flipping it
/// </remarks>
/// <seealso href="Hysteresis.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Hysteresis : AbstractBase
{
    private readonly double _enter;
    private readonly double _exit;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(bool Latched, bool HasValue);
    private State _state, _p_state;

    public override bool IsHot => _state.HasValue;

    /// <summary>
    /// Initializes a new Hysteresis predicate.
    /// </summary>
    /// <param name="enter">Threshold the input must reach to latch true</param>
    /// <param name="exit">Threshold the input must fall to (or below) to latch false; must be &lt;= enter</param>
    public Hysteresis(double enter, double exit)
    {
        if (!double.IsFinite(enter) || !double.IsFinite(exit))
        {
            throw new ArgumentException("enter and exit must be finite", nameof(enter));
        }

        if (exit > enter)
        {
            throw new ArgumentException("exit must be <= enter (upward-latch polarity)", nameof(exit));
        }

        _enter = enter;
        _exit = exit;
        Name = $"Hysteresis({enter},{exit})";
        WarmupPeriod = 0;
        Last = new TValue(0, double.NaN);
    }

    /// <summary>
    /// Initializes a new Hysteresis predicate with source for event-based chaining.
    /// </summary>
    public Hysteresis(ITValuePublisher source, double enter, double exit) : this(enter, exit)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        bool latched = _state.Latched;
        bool hasValue = _state.HasValue;

        if (double.IsFinite(input.Value))
        {
            if (input.Value >= _enter)
            {
                latched = true;
            }
            else if (input.Value <= _exit)
            {
                latched = false;
            }
            hasValue = true;
        }

        _state = new State(latched, hasValue);

        double result;
        if (!hasValue)
        {
            result = double.NaN;
        }
        else
        {
            result = latched ? 1.0 : 0.0;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries(source.Count);
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(times[i], values[i]), true);
            result.Add(tv, true);
        }
        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, double enter, double exit)
    {
        var indicator = new Hysteresis(enter, exit);
        return indicator.Update(source);
    }

    public static (TSeries Results, Hysteresis Indicator) Calculate(TSeries source, double enter, double exit)
    {
        var indicator = new Hysteresis(enter, exit);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = new TValue(0, double.NaN);
    }
}
