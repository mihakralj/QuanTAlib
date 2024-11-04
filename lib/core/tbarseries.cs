using System.Runtime.CompilerServices;

namespace QuanTAlib;

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

    public TSeries Open { get; init; }
    public TSeries High { get; init; }
    public TSeries Low { get; init; }
    public TSeries Close { get; init; }
    public TSeries Volume { get; init; }

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
