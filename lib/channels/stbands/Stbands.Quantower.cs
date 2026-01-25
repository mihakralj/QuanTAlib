using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class StbandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 1, maximum: 1000, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Multiplier", sortIndex: 2, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 1)]
    public double Multiplier { get; set; } = 3.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Stbands? stbands;
    protected LineSeries? UpperSeries;
    protected LineSeries? LowerSeries;
    protected LineSeries? TrendSeries;
    protected LineSeries? WidthSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"STBANDS ({Period},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/stbands/Stbands.cs";

    public StbandsIndicator()
    {
        Name = "STBANDS - Super Trend Bands";
        Description = "ATR-based dynamic support/resistance channel that adapts to price action with trailing stop-loss levels";

        UpperSeries = new("Upper", Color.Red, 2, LineStyle.Solid);
        LowerSeries = new("Lower", Color.Green, 2, LineStyle.Solid);
        TrendSeries = new("Trend", Color.Blue, 1, LineStyle.Dot);
        WidthSeries = new("Width", Color.Gray, 1, LineStyle.Dash);

        AddLineSeries(UpperSeries);
        AddLineSeries(LowerSeries);
        AddLineSeries(TrendSeries);
        AddLineSeries(WidthSeries);

        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        stbands = new(Period, Multiplier);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[0, SeekOriginHistory.End];

        TBar bar = new(
            item.TimeLeft,
            item[PriceType.Open],
            item[PriceType.High],
            item[PriceType.Low],
            item[PriceType.Close],
            item[PriceType.Volume]);

        stbands!.Update(bar, args.IsNewBar());

        UpperSeries!.SetValue(stbands.Upper.Value, stbands.IsHot, ShowColdValues);
        LowerSeries!.SetValue(stbands.Lower.Value, stbands.IsHot, ShowColdValues);
        TrendSeries!.SetValue(stbands.Trend.Value, stbands.IsHot, ShowColdValues);
        WidthSeries!.SetValue(stbands.Width.Value, stbands.IsHot, ShowColdValues);
    }
}