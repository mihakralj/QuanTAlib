using System.Runtime.InteropServices;

namespace QuanTAlib;

public enum Direction : sbyte
{
    Short = -1,
    Flat = 0,
    Long = 1,
}

/// <summary>
/// Timestamped long, flat, or short position state.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TPosition(long Time, Direction Direction, bool IsHot);