using System;

namespace QuanTAlib;

/// <summary>
/// Interface for objects that publish TValue updates.
/// </summary>
public interface ITValuePublisher
{
    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    event Action<TValue> Pub;
}
