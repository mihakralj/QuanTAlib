using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Common lifecycle surface for host-driven signal primitives.
/// Unlike <see cref="AbstractBase"/>, this base does not own indicator inputs,
/// historical priming or batch series updates. It publishes position updates;
/// predicate and order primitives may define their own typed series bases.
/// </summary>
public abstract class SignalBase : ISignalPublisher, IDisposable
{
    public int WarmupPeriod { get; protected init; }

    public TPosition Last { get; protected set; }

    public abstract bool IsHot { get; }

    public event TPositionPublishedHandler? Pub;

    protected void PubEvent(TPosition position, bool isNew = true)
    {
        Pub?.Invoke(this, new TPositionEventArgs { Position = position, IsNew = isNew });
    }

    public abstract TPosition Update(TPosition input, bool isNew = true);

    public abstract void Reset();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

/// <summary>
/// Event arguments for timestamped position updates.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct TPositionEventArgs : IEquatable<TPositionEventArgs>
{
    public TPosition Position { get; init; }
    public bool IsNew { get; init; }

    public bool Equals(TPositionEventArgs other) =>
        Position.Equals(other.Position) && IsNew == other.IsNew;

    public override bool Equals(object? obj) =>
        obj is TPositionEventArgs other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Position, IsNew);

    public static bool operator ==(TPositionEventArgs left, TPositionEventArgs right) => left.Equals(right);

    public static bool operator !=(TPositionEventArgs left, TPositionEventArgs right) => !left.Equals(right);
}

public delegate void TPositionPublishedHandler(object? sender, in TPositionEventArgs args);

public interface ISignalPublisher
{
    event TPositionPublishedHandler? Pub;
}