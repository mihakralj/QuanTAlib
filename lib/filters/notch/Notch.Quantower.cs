using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class NotchIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Q Factor", sortIndex: 2, 0.1, 100, 0.1, 1)]
    public double Q { get; set; } = 1.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Notch? _ma;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 14; // Approximate default
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Notch({Period}, {Q:F1}):{_sourceName}";

    public NotchIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "Notch - Notch Filter";
        Description = "Band-stop filter with a narrow bandwidth.";
        _series = new(name: $"Notch {Period}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ma = new Notch(Period, Q);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        var input = new TValue(item.TimeLeft.Ticks, _priceSelector!(item));
        double value = _ma!.Update(input, isNew).Value;
        _series!.SetValue(value, _ma.IsHot, ShowColdValues);
    }
}