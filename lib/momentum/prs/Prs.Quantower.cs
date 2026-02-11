using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for PRS (Price Relative Strength) indicator.
/// Compares two price sources from the same symbol to identify relative performance.
/// </summary>
/// <remarks>
/// This adapter compares two different price sources from the same symbol (e.g., Close vs Open,
/// Close vs Volume, High vs Low). For cross-symbol relative strength analysis, use the core
/// PRS class directly with data from multiple symbols.
///
/// The output is the ratio of Base/Comparison with optional EMA smoothing.
/// Values above 1.0 indicate the base source is higher than comparison.
/// Rising values indicate base is outperforming, falling values indicate underperformance.
/// </remarks>
[SkipLocalsInit]
public sealed class PrsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Smooth Period", sortIndex: 0, minimum: 1, maximum: 10000)]
    public int SmoothPeriod { get; set; } = 1;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Comparison Source", sortIndex: 2)]
    public SourceType Source2 { get; set; } = SourceType.Open;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Prs _prs = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;
    private Func<IHistoryItem, double> _priceSelector2 = null!;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => SmoothPeriod == 1
        ? $"PRS:{_sourceName}/{Source2}"
        : $"PRS({SmoothPeriod}):{_sourceName}/{Source2}";

    public PrsIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PRS - Price Relative Strength";
        Description = "Compares relative performance between two price sources. Ratio > 1 means base is higher than comparison.";
        _series = new LineSeries(name: "PRS", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _priceSelector2 = Source2.GetPriceSelector();
        _sourceName = Source.ToString();
        _prs = new Prs(SmoothPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();

        // Get both price sources from the same bar
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double baseValue = _priceSelector(item);
        double compValue = _priceSelector2(item);

        var tvalBase = new TValue(item.TimeLeft.Ticks, baseValue);
        var tvalComp = new TValue(item.TimeLeft.Ticks, compValue);

        double value = _prs.Update(tvalBase, tvalComp, isNew).Value;
        _series.SetValue(value, _prs.IsHot, ShowColdValues);
    }
}
