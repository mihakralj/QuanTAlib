using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace QuanTAlib;

public delegate void ValueSignal(object source, in ValueEventArgs args);

[SkipLocalsInit]
public sealed class ValueEventArgs : EventArgs
{
    public readonly TValue Tick;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueEventArgs(TValue value) => Tick = value;
}

[SkipLocalsInit]
public class TSeries : List<TValue>
{
    private static readonly TValue Default = new(DateTime.MinValue, double.NaN);

    public IEnumerable<DateTime> t => this.Select(item => item.t);
    public IEnumerable<double> v => this.Select(item => item.v);
    public TValue Last => Count > 0 ? this[^1] : Default;
    public TValue First => Count > 0 ? this[0] : Default;
    public int Length => Count;
    public string Name { get; set; }

    /// <summary>
    /// Event that publishes value updates to subscribers. This event is used in the pub/sub pattern
    /// where TSeries instances can subscribe to updates from other data sources through the Sub method,
    /// and publish their own updates to downstream subscribers.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S3264:Events should be invoked", Justification = "Event is invoked through delegate")]
    public event ValueSignal Pub = delegate { };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TSeries()
    {
        Name = "Data";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TSeries(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        if (pubEvent != null)
        {
            pubEvent.AddEventHandler(source, new ValueSignal(Sub));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator List<double>(TSeries series) => series.Select(item => item.Value).ToList();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator double[](TSeries series) => series.Select(item => item.Value).ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new virtual void Add(TValue tick)
    {
        if (tick.IsNew || base.Count == 0)
        {
            base.Add(tick);
        }
        else
        {
            this[^1] = tick;
        }
        Pub?.Invoke(this, new ValueEventArgs(tick));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(DateTime Time, double Value, bool IsNew = true, bool IsHot = true) =>
        Add(new TValue(Time, Value, IsNew, IsHot));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(double Value, bool IsNew = true, bool IsHot = true) =>
        Add(new TValue(DateTime.UtcNow, Value, IsNew, IsHot));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        int count = valueList.Count;
        DateTime startTime = DateTime.UtcNow - TimeSpan.FromHours(count);

        for (int i = 0; i < count; i++)
        {
            Add(startTime, valueList[i]);
            startTime = startTime.AddHours(1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TSeries series)
    {
        if (series == this)
        {
            // If adding itself, create a copy to avoid modification during enumeration
            var copy = new TSeries { Name = Name };
            copy.AddRange(this);
            AddRange(copy);
        }
        else
        {
            AddRange(series);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new virtual void AddRange(IEnumerable<TValue> collection)
    {
        foreach (var item in collection)
        {
            Add(item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sub(object source, in ValueEventArgs args) => Add(args.Tick);
}
