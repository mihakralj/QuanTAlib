using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HtitIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 50; // Not used in calculation but kept for consistency

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Htit? _htit;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HTIT:{_sourceName}";

    public HtitIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "HTIT - Ehlers Hilbert Transform Instantaneous Trend";
        Description = "Ehlers Hilbert Transform Instantaneous Trend";
        _series = new LineSeries(name: "HTIT", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _htit = new Htit();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _htit!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _htit.IsHot, ShowColdValues);
    }
}
