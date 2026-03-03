using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class VwapsdIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Number of Deviations", sortIndex: 1, minimum: 0.1, maximum: 5.0, increment: 0.1, decimalPlaces: 1)]
    public double NumDevs { get; set; } = 2.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vwapsd? vwapsd;
    protected LineSeries? VwapSeries;
    protected LineSeries? UpperSeries;
    protected LineSeries? LowerSeries;
    protected LineSeries? WidthSeries;

#pragma warning disable S2325 // Methods and properties that don't access instance data should be static
    public int MinHistoryDepths => 2;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VWAPSD ({NumDevs:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/vwapsd/Vwapsd.cs";

    public VwapsdIndicator()
    {
        Name = "VWAPSD - Volume Weighted Average Price with Configurable Standard Deviation Bands";
        Description = "Volume weighted average price with configurable standard deviation bands";

        VwapSeries = new("VWAP", Color.Blue, 2, LineStyle.Solid);
        UpperSeries = new("Upper", Color.Red, 1, LineStyle.Solid);
        LowerSeries = new("Lower", Color.Green, 1, LineStyle.Solid);
        WidthSeries = new("Width", Color.Gray, 1, LineStyle.Dot);

        AddLineSeries(VwapSeries);
        AddLineSeries(UpperSeries);
        AddLineSeries(LowerSeries);
        AddLineSeries(WidthSeries);

        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        vwapsd = new(NumDevs);
        if (UpperSeries != null)
        {
            UpperSeries.Name = $"Upper (+{NumDevs:F1}σ)";
        }
        if (LowerSeries != null)
        {
            LowerSeries.Name = $"Lower (-{NumDevs:F1}σ)";
        }
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
        TValue result = vwapsd!.Update(bar, args.IsNewBar());

        VwapSeries!.SetValue(result.Value, vwapsd.IsHot, ShowColdValues);
        UpperSeries!.SetValue(vwapsd.Upper.Value, vwapsd.IsHot, ShowColdValues);
        LowerSeries!.SetValue(vwapsd.Lower.Value, vwapsd.IsHot, ShowColdValues);
        WidthSeries!.SetValue(vwapsd.Width.Value, vwapsd.IsHot, ShowColdValues);
    }
}
