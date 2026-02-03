using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for Correlation indicator.
/// Measures the Pearson correlation coefficient between two price series.
/// </summary>
/// <remarks>
/// This adapter compares two different price sources from the same symbol (e.g., Close vs Open,
/// Close vs Volume, High vs Low). For cross-symbol correlation analysis, use the core
/// Correlation class directly with data from multiple symbols.
///
/// The output is the Pearson correlation coefficient, ranging from -1 to +1.
/// Values near +1 indicate strong positive correlation, near -1 indicate strong negative correlation.
/// </remarks>
[SkipLocalsInit]
public sealed class CorrelationIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 2, maximum: 10000)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Source 2 Type", sortIndex: 2)]
    public SourceType Source2 { get; set; } = SourceType.Open;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Correlation _correlation = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;
    private Func<IHistoryItem, double> _priceSelector2 = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CORR({Period}):{_sourceName}/{Source2}";

    public CorrelationIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CORR - Pearson Correlation Coefficient";
        Description = "Measures linear relationship between two price sources. Range: -1 (inverse) to +1 (perfect positive).";
        _series = new LineSeries(name: "Correlation", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _priceSelector2 = Source2.GetPriceSelector();
        _sourceName = Source.ToString();
        _correlation = new Correlation(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();

        // Get both price sources from the same bar
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double valueA = _priceSelector(item);
        double valueB = _priceSelector2(item);

        var tvalA = new TValue(item.TimeLeft.Ticks, valueA);
        var tvalB = new TValue(item.TimeLeft.Ticks, valueB);

        double value = _correlation.Update(tvalA, tvalB, isNew).Value;
        _series.SetValue(value, _correlation.IsHot, ShowColdValues);
    }
}