using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for Kendall Tau-a Rank Correlation indicator.
/// Measures ordinal association between two price sources from the same symbol.
/// </summary>
/// <remarks>
/// This adapter compares two different price sources from the same symbol (e.g., Close vs Open,
/// Close vs Volume, High vs Low). For cross-symbol correlation, use the core
/// Kendall class directly.
///
/// Output is the Kendall Tau-a coefficient, ranging from -1 to +1.
/// Values near +1 indicate strong concordance, near -1 strong discordance.
/// </remarks>
[SkipLocalsInit]
public sealed class KendallIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 2, maximum: 10000)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Source 2 Type", sortIndex: 2)]
    public SourceType Source2 { get; set; } = SourceType.Open;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Kendall _kendall = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;
    private Func<IHistoryItem, double> _priceSelector2 = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"KENDALL({Period}):{_sourceName}/{Source2}";

    public KendallIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "KENDALL - Kendall Tau-a Rank Correlation";
        Description = "Measures ordinal association between two price sources. Range: -1 (discordant) to +1 (concordant).";
        _series = new LineSeries(name: "Kendall", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _priceSelector2 = Source2.GetPriceSelector();
        _sourceName = Source.ToString();
        _kendall = new Kendall(Period);
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

        double value = _kendall.Update(tvalA, tvalB, isNew).Value;
        _series.SetValue(value, _kendall.IsHot, ShowColdValues);
    }
}
