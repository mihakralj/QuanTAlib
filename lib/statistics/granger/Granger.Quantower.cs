using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for Granger Causality indicator.
/// Tests whether one price source Granger-causes another using F-statistic.
/// </summary>
/// <remarks>
/// This adapter compares two different price sources from the same symbol (e.g., Close vs Volume).
/// For cross-symbol Granger causality analysis, use the core Granger class directly.
///
/// Higher F-statistic values indicate stronger evidence that Source 2 Granger-causes Source 1.
/// </remarks>
[SkipLocalsInit]
public sealed class GrangerIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 4, maximum: 10000)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Source 2 Type", sortIndex: 2)]
    public SourceType Source2 { get; set; } = SourceType.Open;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Granger _granger = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;
    private Func<IHistoryItem, double> _priceSelector2 = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"GRANGER({Period}):{_sourceName}/{Source2}";

    public GrangerIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "GRANGER - Granger Causality F-Statistic";
        Description = "Tests whether one price source helps predict another. Higher F-statistic = stronger evidence of Granger causality.";
        _series = new LineSeries(name: "F-Stat", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _priceSelector2 = Source2.GetPriceSelector();
        _sourceName = Source.ToString();
        _granger = new Granger(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();

        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double valueY = _priceSelector(item);
        double valueX = _priceSelector2(item);

        var tvalY = new TValue(item.TimeLeft.Ticks, valueY);
        var tvalX = new TValue(item.TimeLeft.Ticks, valueX);

        double value = _granger.Update(tvalY, tvalX, isNew).Value;
        _series.SetValue(value, _granger.IsHot, ShowColdValues);
    }
}
