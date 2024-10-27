using System.Runtime.CompilerServices;

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

[SkipLocalsInit]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar() : this(DateTime.UtcNow, 0, 0, 0, 0, 0) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(double Open, double High, double Low, double Close, double Volume, bool IsNew = true)
        : this(DateTime.UtcNow, Open, High, Low, Close, Volume, IsNew) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(double value)
        : this(Time: DateTime.UtcNow, Open: value, High: value, Low: value, Close: value, Volume: value, IsNew: true) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(TValue value)
        : this(Time: value.Time, Open: value.Value, High: value.Value, Low: value.Value, Close: value.Value, Volume: value.Value, IsNew: value.IsNew) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(TBar v)
        : this(Time: v.Time, Open: v.Open, High: v.High, Low: v.Low, Close: v.Close, Volume: v.Volume, IsNew: true) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TBar bar) => bar.Close;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TBar tv) => tv.Time;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";
}

public delegate void BarSignal(object source, in TBarEventArgs args);

[SkipLocalsInit]
public sealed class TBarEventArgs : EventArgs
{
    public readonly TBar Bar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBarEventArgs(TBar bar) => Bar = bar;
}

[SkipLocalsInit]
public class TBarSeries : List<TBar>
{
    private static readonly TBar Default = new(DateTime.MinValue, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

    public readonly TSeries Open;
    public readonly TSeries High;
    public readonly TSeries Low;
    public readonly TSeries Close;
    public readonly TSeries Volume;

    public TBar Last => Count > 0 ? this[^1] : Default;
    public TBar First => Count > 0 ? this[0] : Default;
    public int Length => Count;
    public string Name { get; set; }
    public event BarSignal Pub = delegate { };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBarSeries()
    {
        Name = "Bar";
        Open = new TSeries();
        High = new TSeries();
        Low = new TSeries();
        Close = new TSeries();
        Volume = new TSeries();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBarSeries(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new virtual void Add(TBar bar)
    {
        if (bar.IsNew || base.Count == 0)
        {
            base.Add(bar);
        }
        else
        {
            this[^1] = bar;
        }

        Pub?.Invoke(this, new TBarEventArgs(bar));

        Open.Add(bar.Time, bar.Open, IsNew: bar.IsNew, IsHot: true);
        High.Add(bar.Time, bar.High, IsNew: bar.IsNew, IsHot: true);
        Low.Add(bar.Time, bar.Low, IsNew: bar.IsNew, IsHot: true);
        Close.Add(bar.Time, bar.Close, IsNew: bar.IsNew, IsHot: true);
        Volume.Add(bar.Time, bar.Volume, IsNew: bar.IsNew, IsHot: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(DateTime Time, double Open, double High, double Low, double Close, double Volume, bool IsNew = true) =>
        Add(new TBar(Time, Open, High, Low, Close, Volume, IsNew));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(double Open, double High, double Low, double Close, double Volume, bool IsNew = true) =>
        Add(new TBar(DateTime.Now, Open, High, Low, Close, Volume, IsNew));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TBarSeries series)
    {
        if (series == this)
        {
            // If adding itself, create a copy to avoid modification during enumeration
            var copy = new TBarSeries { Name = Name };
            copy.AddRange(this);
            AddRange(copy);
        }
        else
        {
            AddRange(series);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new virtual void AddRange(IEnumerable<TBar> collection)
    {
        foreach (var item in collection)
        {
            Add(item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sub(object source, in TBarEventArgs args)
    {
        Add(args.Bar);
    }
}
