using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class LoessIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 3, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Loess? _ma;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Loess({Period}):{_sourceName}";

    public LoessIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "Loess - Locally Estimated Scatterplot Smoothing";
        Description = "Locally Estimated Scatterplot Smoothing";
        _series = new LineSeries(name: "Loess", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ma = new Loess(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _ma!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _ma.IsHot, ShowColdValues);
    }
}