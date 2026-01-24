using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class BbandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 2, maximum: 1000, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 2, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 1)]
    public double Multiplier { get; set; } = 2.0;

    [IndicatorExtensions.DataSourceInput(sortIndex: 3)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Bbands? bbands;
    protected LineSeries? MiddleSeries;
    protected LineSeries? UpperSeries;
    protected LineSeries? LowerSeries;
    protected LineSeries? WidthSeries;
    protected LineSeries? PercentBSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BBANDS ({Period},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/bbands/Bbands.cs";

    public BbandsIndicator()
    {
        Name = "BBANDS - Bollinger Bands";
        Description = "Volatility-based channel indicator with upper and lower bands positioned at a specified number of standard deviations from a moving average";

        MiddleSeries = new("Middle", Color.Blue, 2, LineStyle.Solid);
        UpperSeries = new("Upper", Color.Red, 1, LineStyle.Solid);
        LowerSeries = new("Lower", Color.Green, 1, LineStyle.Solid);
        WidthSeries = new("Width", Color.Gray, 1, LineStyle.Dot);
        PercentBSeries = new("%B", Color.Purple, 1, LineStyle.Dash);

        AddLineSeries(MiddleSeries);
        AddLineSeries(UpperSeries);
        AddLineSeries(LowerSeries);
        AddLineSeries(WidthSeries);
        AddLineSeries(PercentBSeries);

        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        bbands = new(Period, Multiplier);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double price = priceSelector(item);
        var time = HistoricalData.Time();
        
        TValue input = new(time, price);
        TValue result = bbands!.Update(input, args.IsNewBar());

        MiddleSeries!.SetValue(result.Value, bbands.IsHot, ShowColdValues);
        UpperSeries!.SetValue(bbands.Upper.Value, bbands.IsHot, ShowColdValues);
        LowerSeries!.SetValue(bbands.Lower.Value, bbands.IsHot, ShowColdValues);
        WidthSeries!.SetValue(bbands.Width.Value, bbands.IsHot, ShowColdValues);
        PercentBSeries!.SetValue(bbands.PercentB.Value, bbands.IsHot, ShowColdValues);
    }
}