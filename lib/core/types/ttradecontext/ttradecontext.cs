using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Host-supplied description of an open trade. This is an input the host provides at the
/// entry fill (via <c>Arm</c> on a stop or sizer node), not a stream the library computes.
/// </summary>
/// <remarks>
/// <para><c>InitialStop</c> defines 1R as <c>|EntryPrice - InitialStop|</c>. This is the unit
/// every R-multiple stop, target, and sizing function in <c>stops/</c> and <c>sizing/</c>
/// measures against.</para>
/// <para>The library never infers a <see cref="TTradeContext"/> from an entry signal: arming a
/// stop or sizer node with a fresh context is always an explicit host action at the fill, so
/// that an extra entry signal received while already in a position cannot silently reset
/// trade-anchored state (the failure mode this type's arming model is designed to prevent).</para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TTradeContext(
    long EntryTime,
    double EntryPrice,
    Direction Side,
    double InitialStop,
    double PointValue,
    double TickSize);

/// <summary>
/// Touch state reported by a <c>stops/</c> node against its previously computed levels.
/// Values may combine: an intrabar bar range can reach both the stop and the target.
/// </summary>
[Flags]
[SuppressMessage("Design", "S2344:Rename this enumeration to remove the 'Flags' suffix", Justification = "Name is fixed by the strategy-primitives specification (Section 8.2); .NET itself has precedent (e.g. System.Reflection.BindingFlags).")]
public enum BracketFlags : byte
{
    /// <summary>No level was touched on this bar.</summary>
    None = 0,

    /// <summary>The bar range reached the previously computed stop level.</summary>
    StopTouched = 1,

    /// <summary>The bar range reached the previously computed target level.</summary>
    TargetTouched = 2,

    /// <summary>Both the stop and the target were reached on the same bar; the intrabar order
    /// is unknown without lower-timeframe data.</summary>
    BothTouched = StopTouched | TargetTouched,

    /// <summary>The bar's open was already beyond the level; a realistic fill is near the open,
    /// not at the level. The library reports this; the host prices the fill.</summary>
    GapThrough = 4,

    /// <summary>The touch occurred on the entry bar. Whether this counts is a constructor
    /// option on the stop node (default off).</summary>
    EntryBar = 8,
}

/// <summary>
/// Why a <c>stops/</c> node reports a position should exit. The library reports the reason;
/// the host decides whether and how to act on it.
/// </summary>
public enum ExitReason : byte
{
    /// <summary>No exit condition is active.</summary>
    None = 0,

    /// <summary>The initial invalidation stop was touched.</summary>
    InitialStop,

    /// <summary>A structural level (a confirmed swing) was touched.</summary>
    Structural,

    /// <summary>A trailing stop was touched.</summary>
    Trail,

    /// <summary>A breakeven-adjusted stop was touched.</summary>
    Breakeven,

    /// <summary>A profit target was reached.</summary>
    Target,

    /// <summary>A fixed-bar time stop elapsed.</summary>
    Time,

    /// <summary>A "no progress" stale-trade exit fired.</summary>
    Stale,

    /// <summary>A session-time flatten exit fired.</summary>
    Session,
}
