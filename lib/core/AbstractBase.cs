using System;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Abstract base class for all indicators.
/// Enforces a consistent contract for State, Name, WarmupPeriod, and core methods.
/// </summary>
public abstract class AbstractBase : ITValuePublisher
{
    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; protected set; } = string.Empty;

    /// <summary>
    /// Number of periods before the indicator is considered "hot" (valid).
    /// </summary>
    public int WarmupPeriod { get; protected set; }

    /// <summary>
    /// Current value of the indicator.
    /// </summary>
    public TValue Last { get; protected set; }

    /// <summary>
    /// True if the indicator has enough data to produce valid results.
    /// </summary>
    public abstract bool IsHot { get; }

    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Helper to invoke the Pub event.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void PubEvent(TValue value, bool isNew = true)
    {
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });
    }

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    /// <param name="step">Time interval between values (default: 1 second)</param>
    public abstract void Prime(ReadOnlySpan<double> source, TimeSpan? step = null);

    /// <summary>
    /// Updates the indicator with a single value.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True if this is a new bar, False if it's an update to the last bar</param>
    /// <returns>Updated value</returns>
    public abstract TValue Update(TValue input, bool isNew = true);

    /// <summary>
    /// Updates the indicator with a series of values.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>Series of calculated values</returns>
    public abstract TSeries Update(TSeries source);

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    public abstract void Reset();
}
