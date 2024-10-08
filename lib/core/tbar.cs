namespace QuanTAlib;

public interface ITBar
{
    DateTime Time { get; }
    double Open { get; }
    double High { get; }
    double Low { get; }
    double Close { get; }
    double Volume { get; }
    bool IsNew { get; }
}

public readonly record struct TBar(DateTime Time, double Open, double High, double Low, double Close, double Volume, bool IsNew = true) : ITBar
{
    public DateTime Time { get; init; } = Time;
public double Open { get; init; } = Open;
public double High { get; init; } = High;
public double Low { get; init; } = Low;
public double Close { get; init; } = Close;
public double Volume { get; init; } = Volume;
public bool IsNew { get; init; } = IsNew;

public double HL2 => (High + Low) * 0.5;
public double OC2 => (Open + Close) * 0.5;
public double OHL3 => (Open + High + Low) / 3;
public double HLC3 => (High + Low + Close) / 3;
public double OHLC4 => (Open + High + Low + Close) * 0.25;
public double HLCC4 => (High + Low + Close + Close) * 0.25;

public TBar() : this(DateTime.UtcNow, 0, 0, 0, 0, 0) { }
public TBar(double Open, double High, double Low, double Close, double Volume, bool IsNew = true) : this(DateTime.UtcNow, Open, High, Low, Close, Volume, IsNew) { }
public TBar(double value) : this(Time: DateTime.UtcNow, Open: value, High: value, Low: value, Close: value, Volume: value, IsNew: true) { }
public TBar(TValue value) : this(Time: value.Time, Open: value.Value, High: value.Value, Low: value.Value, Close: value.Value, Volume: value.Value, IsNew: value.IsNew) { }

public static implicit operator double(TBar bar) => bar.Close;
public static implicit operator DateTime(TBar tv) => tv.Time;
public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";
}

public delegate void BarSignal(object source, in TBarEventArgs args);

public class TBarEventArgs : EventArgs
{
    public TBar Bar { get; }
    public TBarEventArgs(TBar bar) { Bar = bar; }
}

public class TBarSeries : List<TBar>
{
    private readonly TBar Default = new(DateTime.MinValue, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

    public TSeries Open;
    public TSeries High;
    public TSeries Low;
    public TSeries Close;
    public TSeries Volume;


    public TBar Last => Count > 0 ? this[^1] : Default;
    public TBar First => Count > 0 ? this[0] : Default;
    public int Length => Count;
    public string Name { get; set; }
    public event BarSignal Pub = delegate { };

    public TBarSeries()
    {
        this.Name = "Bar";
        (Open, High, Low, Close, Volume) = ([], [], [], [], []);

    }
    public TBarSeries(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    public new virtual void Add(TBar bar)
    {
        if (bar.IsNew || base.Count == 0) { base.Add(bar); }
        else { this[^1] = bar; }
        Pub?.Invoke(this, new TBarEventArgs(bar));

        Open.Add(bar.Time, bar.Open, IsNew: bar.IsNew, IsHot: true);
        High.Add(bar.Time, bar.High, IsNew: bar.IsNew, IsHot: true);
        Low.Add(bar.Time, bar.Low, IsNew: bar.IsNew, IsHot: true);
        Close.Add(bar.Time, bar.Close, IsNew: bar.IsNew, IsHot: true);
        Volume.Add(bar.Time, bar.Volume, IsNew: bar.IsNew, IsHot: true);
    }
    public void Add(DateTime Time, double Open, double High, double Low, double Close, double Volume, bool IsNew = true) =>
        this.Add(new TBar(Time, Open, High, Low, Close, Volume, IsNew));

    public void Add(double Open, double High, double Low, double Close, double Volume, bool IsNew = true) =>
        this.Add(new TBar(DateTime.Now, Open, High, Low, Close, Volume, IsNew));

    public void Add(TBarSeries series)
    {
        if (series == this)
        {
            // If adding itself, create a copy to avoid modification during enumeration
            var copy = new TBarSeries { Name = this.Name };
            copy.AddRange(this);
            AddRange(copy);
        }
        else
        {
            AddRange(series);
        }
    }
    public new virtual void AddRange(IEnumerable<TBar> collection)
    {
        foreach (var item in collection)
        {
            Add(item);
        }
    }

    public void Sub(object source, in TBarEventArgs args)
    {
        Add(args.Bar);
    }
}