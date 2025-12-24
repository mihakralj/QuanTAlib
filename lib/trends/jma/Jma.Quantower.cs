using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class JmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Phase", sortIndex: 2, -100, 100, 1, 0)]
    public int Phase { get; set; } = 0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    [InputParameter("Color", sortIndex: 22)]
    public Color LineColor { get; set; } = IndicatorExtensions.Averages;

    [InputParameter("Width", sortIndex: 23)]
    public int LineWidth { get; set; } = 2;

    private Jma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"JMA {Period}:{Phase}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/jma/Jma.Quantower.cs";

    public JmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "JMA - Jurik Moving Average";
        Description = "Jurik Moving Average";
        Series = new(name: $"JMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Jma(Period, Phase);
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
