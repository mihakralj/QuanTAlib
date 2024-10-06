namespace QuanTAlib;

public interface ITValue
{
    DateTime Time { get; }
    double Value { get; }
    bool IsNew { get; }
    bool IsHot { get; }
}

public readonly record struct TValue(DateTime Time, double Value, bool IsNew = true, bool IsHot = true) : ITValue
{
    public DateTime Time { get; init; } = Time;
    public double Value { get; init; } = Value;
    public bool IsNew { get; init; } = IsNew;
    public bool IsHot { get; init; } = IsHot;
    public DateTime t => Time;
    public double v => Value;

    public TValue() : this(DateTime.UtcNow, 0) { }
    public TValue(double value, bool isNew = true, bool isHot = true) : this(DateTime.UtcNow, value, IsNew: isNew, IsHot: isHot) { }
    public static implicit operator double(TValue tv) => tv.Value;
    public static implicit operator DateTime(TValue tv) => tv.Time;
    public static implicit operator TValue(double value) => new TValue(DateTime.UtcNow, value);

    public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}, {Value:F2}, IsNew: {IsNew}, IsHot: {IsHot}]";
}

public delegate void ValueSignal(object source, in ValueEventArgs args);

public class ValueEventArgs : EventArgs
{
    public TValue Tick { get; }
    public ValueEventArgs(TValue value) { Tick = value; }
}

public class TSeries : List<TValue>
{
    private readonly TValue Default = new(DateTime.MinValue, double.NaN);
    public IEnumerable<DateTime> t => this.Select(item => item.t);
    public IEnumerable<double> v => this.Select(item => item.v);
    public TValue Last => Count > 0 ? this[^1] : Default;
    public TValue First => Count > 0 ? this[0] : Default;
    public int Length => Count;
    public string Name { get; set; }
    public event ValueSignal Pub = delegate { };

    public TSeries() { this.Name = "Data"; }

    public TSeries(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        if (pubEvent != null)
        {
            /*
            var nameProperty = source.GetType().GetProperty("Name");
            if (nameProperty != null) {
                Name = nameProperty.GetValue(nameProperty)?.ToString()!;
            }
            */
            pubEvent.AddEventHandler(source, new ValueSignal(Sub));
        }
    }
    public static explicit operator List<double>(TSeries series) => series.Select(item => item.Value).ToList();
    public static explicit operator double[](TSeries series) => series.Select(item => item.Value).ToArray();

    public new virtual void Add(TValue tick)
    {
        if (tick.IsNew || base.Count==0) { base.Add(tick); }
        else { this[^1] = tick; }
        Pub?.Invoke(this, new ValueEventArgs(tick));
    }
    public virtual void Add(DateTime Time, double Value, bool IsNew = true, bool IsHot = true) => this.Add(new TValue(Time, Value, IsNew, IsHot));
    public virtual void Add(double Value, bool IsNew = true, bool IsHot = true) => this.Add(new TValue(DateTime.UtcNow, Value, IsNew, IsHot));

    public void Add(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        int count = valueList.Count;
        DateTime startTime = DateTime.UtcNow - TimeSpan.FromHours(count);

        for (int i = 0; i < count; i++)
        {
            this.Add(startTime, valueList[i]);
            startTime = startTime.AddHours(1);
        }
    }
    public void Add(TSeries series)
    {
        if (series == this)
        {
            // If adding itself, create a copy to avoid modification during enumeration
            var copy = new TSeries { Name = this.Name };
            copy.AddRange(this);
            AddRange(copy);
        }
        else
        {
            AddRange(series);
        }
    }
    public new virtual void AddRange(IEnumerable<TValue> collection)
    {
        foreach (var item in collection)
        {
            Add(item);
        }
    }
    public void Sub(object source, in ValueEventArgs args) { Add(args.Tick); }
}