using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Wraps any <see cref="ITValuePublisher"/> so that downstream consumers only see a bar's value
/// once it is confirmed (closed), rather than on every intrabar tick.
/// </summary>
/// <remarks>
/// <para>The library cannot know a bar is final until the next <c>isNew == true</c> update
/// arrives for the following bar (per the strategy-primitives specification, Section 8.6). This
/// is the equivalent of Pine Script's <c>barstate.isconfirmed</c> gating: hosts that want to
/// alert intrabar subscribe to the raw, wrapped node directly; hosts that want to act only at
/// bar close subscribe to a <see cref="ConfirmOnClose"/> wrapping it instead.</para>
///
/// <para><b>Behavior:</b> every intrabar tick for the currently forming bar (whether the first
/// tick, marked <c>isNew = true</c>, or a correction, marked <c>isNew = false</c>) is buffered
/// but not published. The buffered value is published — stamped with the closed bar's own
/// <c>Time</c>, marked <c>isNew = true</c> — exactly once, at the moment the *next* bar's first
/// tick arrives. That next tick then becomes the new pending value, and the cycle repeats. This
/// means <see cref="ConfirmOnClose"/> is always exactly one bar behind its source: it publishes
/// bar t's settled value when bar t+1 opens, never when bar t itself opens.</para>
///
/// <para><b>Cold state:</b> before the first confirmation, <see cref="IsHot"/> is false and
/// <see cref="AbstractBase.Last"/> is the default (NaN) value; no event is published while
/// cold, since publishing on every raw tick would defeat the purpose of this wrapper.</para>
/// </remarks>
[SkipLocalsInit]
public sealed class ConfirmOnClose : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(TValue Pending, bool HasPending, bool HasConfirmed);

    private State _state;
    private State _p_state;

    /// <summary>
    /// True once at least one bar has been confirmed. Before that, no value has ever been
    /// published and <see cref="AbstractBase.Last"/> is the default (NaN) value.
    /// </summary>
    public override bool IsHot => _state.HasConfirmed;

    /// <summary>
    /// Creates a ConfirmOnClose node for direct pull-style updates.
    /// </summary>
    public ConfirmOnClose()
    {
        Name = "ConfirmOnClose";
        WarmupPeriod = 0;
        Last = new TValue(0, double.NaN);
    }

    /// <summary>
    /// Creates a ConfirmOnClose node chained to an upstream publisher.
    /// </summary>
    /// <param name="source">Source indicator whose confirmed (closed-bar) values are republished</param>
    public ConfirmOnClose(ITValuePublisher source) : this()
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

        if (isNew && _state.HasPending)
        {
            // The previous bar's forming value is now settled: publish it as confirmed.
            TValue confirmed = _state.Pending;
            _state = new State(input, true, true);
            Last = confirmed;
            PubEvent(confirmed, isNew: true);
            return confirmed;
        }

        // Still buffering the forming bar (first tick ever, or any subsequent tick/correction
        // of the current bar): remember it, but do not publish yet.
        _state = new State(input, true, _state.HasConfirmed);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries();
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(times[i], values[i]), true);
            if (IsHot)
            {
                result.Add(tv, true);
            }
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

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = new TValue(0, double.NaN);
    }
}
