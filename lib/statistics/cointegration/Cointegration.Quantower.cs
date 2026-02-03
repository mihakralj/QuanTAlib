using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for Cointegration indicator.
/// Measures the statistical equilibrium relationship between two price series
/// using the Engle-Granger two-step method with ADF test.
/// </summary>
/// <remarks>
/// This adapter compares two different price sources from the same symbol (e.g., Close vs Open,
/// Close vs Volume, High vs Low). For cross-symbol cointegration analysis, use the core
/// Cointegration class directly with data from multiple symbols.
///
/// The output is the ADF test statistic. More negative values indicate stronger cointegration.
/// Critical values: -3.43 (1%), -2.86 (5%), -2.57 (10%)
/// </remarks>
[SkipLocalsInit]
public sealed class CointegrationIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 2, maximum: 10000)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Source 2 Type", sortIndex: 2)]
    public SourceType Source2 { get; set; } = SourceType.Open;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cointegration _cointegration = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;
    private Func<IHistoryItem, double> _priceSelector2 = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"COINT({Period}):{_sourceName}/{Source2}";

    public CointegrationIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "COINT - Cointegration (Engle-Granger)";
        Description = "Measures statistical equilibrium between two price sources using ADF test. More negative = stronger cointegration.";
        _series = new LineSeries(name: "ADF", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _priceSelector2 = Source2.GetPriceSelector();
        _sourceName = Source.ToString();
        _cointegration = new Cointegration(Period);
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

        double value = _cointegration.Update(tvalA, tvalB, isNew).Value;
        _series.SetValue(value, _cointegration.IsHot, ShowColdValues);
    }
}
