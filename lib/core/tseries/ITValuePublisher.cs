using System;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Performance-focused event args for TValue updates.
/// Implemented as struct to avoid heap allocations in high-frequency event dispatch.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct TValueEventArgs
{
    public TValue Value { get; init; }
    public bool IsNew { get; init; }
}

// Performance-focused event args struct; not derived from EventArgs by design.
// We intentionally deviate from the standard EventArgs pattern here for perf.
// MA0046 suppressed: struct-based args avoid heap allocations in high-frequency events.
#pragma warning disable MA0046 // The second parameter must be of type 'System.EventArgs' or a derived type
public delegate void TValuePublishedHandler(object? sender, in TValueEventArgs args);

/// <summary>
/// Interface for objects that publish TValue updates.
/// </summary>
public interface ITValuePublisher
{
    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    event TValuePublishedHandler? Pub;
}
#pragma warning restore MA0046
