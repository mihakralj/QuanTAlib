using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Order action represented by a strategy signal. Execution remains host-owned.
/// </summary>
public enum OrderAction : sbyte
{
    BTO = 2,
    BTC = 1,
    NIL = 0,
    STC = -1,
    STO = -2,
}

/// <summary>
/// Timestamped order action signal.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TOrder(long Time, OrderAction Action, bool IsHot);