using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class VwapbandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Multiplier", sortIndex: 1, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 1)]
    public double Multiplier { get; set; } = 1.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vwapbands? vwapbands;
    protected LineSeries? VwapSeries;
    protected LineSeries? Upper1Series;
    protected LineSeries? Lower1Series;
    protected LineSeries? Upper2Series;
    protected LineSeries? Lower2Series;
    protected LineSeries? WidthSeries;

#pragma warning disable S2325 // Methods and properties that don't access instance data should be static
    public int MinHistoryDepths => 2;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VWAPBANDS ({Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/vwapbands/Vwapbands.cs";

    public VwapbandsIndicator()
    {
        Name = "VWAPBANDS - Volume Weighted Average Price with Standard Deviation Bands";
        Description = "Volume weighted average price with 1σ and 2σ standard deviation bands";

        VwapSeries = new("VWAP", Color.Blue, 2, LineStyle.Solid);
        Upper1Series = new("Upper1 (+1σ)", Color.Red, 1, LineStyle.Solid);
        Lower1Series = new("Lower1 (-1σ)", Color.Green, 1, LineStyle.Solid);
        Upper2Series = new("Upper2 (+2σ)", Color.Orange, 1, LineStyle.Dot);
        Lower2Series = new("Lower2 (-2σ)", Color.Cyan, 1, LineStyle.Dot);
        WidthSeries = new("Width", Color.Gray, 1, LineStyle.Dot);

        AddLineSeries(VwapSeries);
        AddLineSeries(Upper1Series);
        AddLineSeries(Lower1Series);
        AddLineSeries(Upper2Series);
        AddLineSeries(Lower2Series);
        AddLineSeries(WidthSeries);

        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        vwapbands = new(Multiplier);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[0, SeekOriginHistory.End];

        // VWAP requires OHLCV data - using HLC3 for price
        double high = item[PriceType.High];
        double low = item[PriceType.Low];
        double close = item[PriceType.Close];
        double volume = item[PriceType.Volume];

        TBar bar = new(item.TimeLeft, item[PriceType.Open], high, low, close, volume);
        TValue result = vwapbands!.Update(bar, args.IsNewBar());

        VwapSeries!.SetValue(result.Value, vwapbands.IsHot, ShowColdValues);
        Upper1Series!.SetValue(vwapbands.Upper1.Value, vwapbands.IsHot, ShowColdValues);
        Lower1Series!.SetValue(vwapbands.Lower1.Value, vwapbands.IsHot, ShowColdValues);
        Upper2Series!.SetValue(vwapbands.Upper2.Value, vwapbands.IsHot, ShowColdValues);
        Lower2Series!.SetValue(vwapbands.Lower2.Value, vwapbands.IsHot, ShowColdValues);
        WidthSeries!.SetValue(vwapbands.Width.Value, vwapbands.IsHot, ShowColdValues);
    }
}