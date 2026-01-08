using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class WienerIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Smooth Period", sortIndex: 2, 2, 1000, 1, 0)]
    public int SmoothPeriod { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Wiener? _wiener;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Wiener({Period},{SmoothPeriod}):{_sourceName}";

    public WienerIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "Wiener - Wiener Filter";
        Description = "Adaptive smoothing filter";
        _series = new(name: $"Wiener {Period}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        
        _wiener = new Wiener(Period, SmoothPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _wiener!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _wiener.IsHot, ShowColdValues);
    }
}