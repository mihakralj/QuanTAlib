using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class UchannelIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("STR Period", sortIndex: 1, minimum: 1, maximum: 1000, increment: 1, decimalPlaces: 0)]
    public int StrPeriod { get; set; } = 20;

    [InputParameter("Center Period", sortIndex: 2, minimum: 1, maximum: 1000, increment: 1, decimalPlaces: 0)]
    public int CenterPeriod { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 3, minimum: 0.001, maximum: 10.0, increment: 0.1, decimalPlaces: 3)]
    public double Multiplier { get; set; } = 1.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Uchannel? uchannel;
    protected LineSeries? MiddleSeries;
    protected LineSeries? UpperSeries;
    protected LineSeries? LowerSeries;
    protected LineSeries? StrSeries;
    protected LineSeries? WidthSeries;

    public int MinHistoryDepths => Math.Max(StrPeriod, CenterPeriod);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"UCHANNEL ({StrPeriod},{CenterPeriod},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/uchannel/Uchannel.cs";

    public UchannelIndicator()
    {
        Name = "UCHANNEL - Ehlers Ultimate Channel";
        Description = "Volatility channel using the Ehlers Ultrasmooth Filter (USF) for both centerline and True Range smoothing";

        MiddleSeries = new("Middle", Color.Blue, 2, LineStyle.Solid);
        UpperSeries = new("Upper", Color.Red, 1, LineStyle.Solid);
        LowerSeries = new("Lower", Color.Green, 1, LineStyle.Solid);
        StrSeries = new("STR", Color.Orange, 1, LineStyle.Dot);
        WidthSeries = new("Width", Color.Gray, 1, LineStyle.Dot);

        AddLineSeries(MiddleSeries);
        AddLineSeries(UpperSeries);
        AddLineSeries(LowerSeries);
        AddLineSeries(StrSeries);
        AddLineSeries(WidthSeries);

        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        uchannel = new(StrPeriod, CenterPeriod, Multiplier);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[0, SeekOriginHistory.End];
        double open = item[PriceType.Open];
        double high = item[PriceType.High];
        double low = item[PriceType.Low];
        double close = item[PriceType.Close];

        TBar input = new(item.TimeLeft, open, high, low, close, item[PriceType.Volume]);
        TValue result = uchannel!.Update(input, args.IsNewBar());

        MiddleSeries!.SetValue(result.Value, uchannel.IsHot, ShowColdValues);
        UpperSeries!.SetValue(uchannel.Upper.Value, uchannel.IsHot, ShowColdValues);
        LowerSeries!.SetValue(uchannel.Lower.Value, uchannel.IsHot, ShowColdValues);
        StrSeries!.SetValue(uchannel.STR.Value, uchannel.IsHot, ShowColdValues);
        WidthSeries!.SetValue(uchannel.Width.Value, uchannel.IsHot, ShowColdValues);
    }
}
