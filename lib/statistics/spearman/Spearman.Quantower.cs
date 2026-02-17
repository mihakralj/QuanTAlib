using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for Spearman Rank Correlation indicator.
/// Measures monotonic association between two price sources from the same symbol.
/// </summary>
/// <remarks>
/// This adapter compares two different price sources from the same symbol (e.g., Close vs Open,
/// Close vs Volume, High vs Low). For cross-symbol correlation, use the core
/// Spearman class directly.
///
/// Output is Spearman's ρ coefficient, ranging from -1 to +1.
/// Values near +1 indicate strong positive monotonic association, near -1 strong negative.
/// </remarks>
[SkipLocalsInit]
public sealed class SpearmanIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 2, maximum: 10000)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Source 2 Type", sortIndex: 2)]
    public SourceType Source2 { get; set; } = SourceType.Open;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Spearman _spearman = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;
    private Func<IHistoryItem, double> _priceSelector2 = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SPEARMAN({Period}):{_sourceName}/{Source2}";

    public SpearmanIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SPEARMAN - Spearman Rank Correlation";
        Description = "Measures monotonic association between two price sources. Range: -1 to +1.";
        _series = new LineSeries(name: "Spearman", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _priceSelector2 = Source2.GetPriceSelector();
        _sourceName = Source.ToString();
        _spearman = new Spearman(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();

        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double valueA = _priceSelector(item);
        double valueB = _priceSelector2(item);

        var tvalA = new TValue(item.TimeLeft.Ticks, valueA);
        var tvalB = new TValue(item.TimeLeft.Ticks, valueB);

        double value = _spearman.Update(tvalA, tvalB, isNew).Value;
        _series.SetValue(value, _spearman.IsHot, ShowColdValues);
    }
}
