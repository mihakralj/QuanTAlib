using System;

namespace QuanTAlib;

public sealed class TValueEventArgs : EventArgs
{
    public TValue Value { get; init; }
    public bool IsNew { get; init; }
}

public delegate void TValuePublishedHandler(object? sender, TValueEventArgs args);

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
