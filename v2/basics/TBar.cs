//namespace QuanTAlib;
public readonly record struct TBar(DateTime Time, double Open, double High, double Low, double Close, double Volume, bool IsNew = true)
{
    public DateTime Time { get; init; } = Time;
    public double Open { get; init; } = Open;
    public double High { get; init; } = High;
    public double Low { get; init; } = Low;
    public double Close { get; init; } = Close;
    public double Volume { get; init; } = Volume;
    public bool IsNew { get; init; } = IsNew;

    // Aliases
    public DateTime t => Time;
    public double o => Open;
    public double h => High;
    public double l => Low;
    public double c => Close;
    public double v => Volume;
    public bool n => IsNew;

    public TBar() : this(DateTime.UtcNow, 0, 0, 0, 0, 0) { }
    public TBar(double open, double high, double low, double close, double volume) : this(DateTime.UtcNow, open, high, low, close, volume) { }
    public TBar((DateTime time, double open, double high, double low, double close, double volume) tuple) : this(tuple.time, tuple.open, tuple.high, tuple.low, tuple.close, tuple.volume) { }

    public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";
}


public delegate void BarSignal(object source, BarEventArgs args);


public class BarEventArgs : EventArgs {
    public TBar Bar { get; }
    // Alternative names for Bar
    public TBar Candle => Bar;
    public TBar PriceBar => Bar;
    public TBar OHLCV => Bar;
    public TBar OHLC => Bar;
    public TBar TradingBar => Bar;
    public TBar Quote => Bar;
    public BarEventArgs(TBar bar) { Bar = bar; }
}