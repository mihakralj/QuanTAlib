using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class DsmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Scale Factor", sortIndex: 2, 0.01, 0.9, 0.01, 2)]
    public double ScaleFactor { get; set; } = 0.5;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    [InputParameter("Color", sortIndex: 22)]
    public Color LineColor { get; set; } = IndicatorExtensions.Averages;

    [InputParameter("Width", sortIndex: 23)]
    public int LineWidth { get; set; } = 2;

    private Dsma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DSMA {Period}:{ScaleFactor:F2}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_IIR/dsma/Dsma.Quantower.cs";

    public DsmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "DSMA - Deviation-Scaled Moving Average";
        Description = "Deviation-Scaled Moving Average with Super Smoother filter";
        Series = new LineSeries(name: $"DSMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Dsma(Period, ScaleFactor);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        Series!.Color = LineColor;
        Series!.Width = LineWidth;
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        TValue result = ma!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew: args.IsNewBar());

        Series!.SetValue(result.Value, ma.IsHot, ShowColdValues);
    }
}
