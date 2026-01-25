using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class UbandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 1, maximum: 1000, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 2, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 1)]
    public double Multiplier { get; set; } = 1.0;

    [IndicatorExtensions.DataSourceInput(sortIndex: 3)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ubands? ubands;
    protected LineSeries? MiddleSeries;
    protected LineSeries? UpperSeries;
    protected LineSeries? LowerSeries;
    protected LineSeries? WidthSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"UBANDS ({Period},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/ubands/Ubands.cs";

    public UbandsIndicator()
    {
        Name = "UBANDS - Ehlers Ultimate Bands";
        Description = "Volatility channel using the Ehlers Ultrasmooth Filter (USF) as the middle band with RMS-based bands";

        MiddleSeries = new("Middle", Color.Blue, 2, LineStyle.Solid);
        UpperSeries = new("Upper", Color.Red, 1, LineStyle.Solid);
        LowerSeries = new("Lower", Color.Green, 1, LineStyle.Solid);
        WidthSeries = new("Width", Color.Gray, 1, LineStyle.Dot);

        AddLineSeries(MiddleSeries);
        AddLineSeries(UpperSeries);
        AddLineSeries(LowerSeries);
        AddLineSeries(WidthSeries);

        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        ubands = new(Period, Multiplier);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double price = priceSelector(item);
        var time = HistoricalData.Time();

        TValue input = new(time, price);
        TValue result = ubands!.Update(input, args.IsNewBar());

        MiddleSeries!.SetValue(result.Value, ubands.IsHot, ShowColdValues);
        UpperSeries!.SetValue(ubands.Upper.Value, ubands.IsHot, ShowColdValues);
        LowerSeries!.SetValue(ubands.Lower.Value, ubands.IsHot, ShowColdValues);
        WidthSeries!.SetValue(ubands.Width.Value, ubands.IsHot, ShowColdValues);
    }
}