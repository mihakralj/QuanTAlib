/*
public class TickPub
{
    public event Signal? OnTick;
    public TValue CurrentValue { get; private set; }

    internal void RaiseEvent(object sender, ValueEventArgs args) {
        CurrentValue = args.Data;
        OnTick?.Invoke(sender, args);
    }

    public void Subscribe(Signal handler) {
        OnTick += handler;
    }

    public void Raise(object sender, ValueEventArgs args) {
        RaiseEvent(sender, args);
    }

    public static explicit operator double(TickPub tp) {
        return tp.CurrentValue.v;
    }
}
*/