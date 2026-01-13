using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class MmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Mma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MMA {Period}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_IIR/mma/Mma.Quantower.cs";

    public MmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "MMA - Modified Moving Average";
        Description = "Modified Moving Average blending SMA with a weighted component.";
        Series = new LineSeries(name: $"MMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Mma(Period);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
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
