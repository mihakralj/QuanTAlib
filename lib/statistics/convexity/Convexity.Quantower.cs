using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ConvexityIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Convexity _convexity = null!;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    // For dual-input, we use Close as asset and Open as market proxy
    // (In real use, user would customize the market data source)
    private readonly LineSeries _convexitySeries;
    private readonly LineSeries _betaStdSeries;
    private readonly LineSeries _betaUpSeries;
    private readonly LineSeries _betaDownSeries;
    private readonly LineSeries _ratioSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CONVEXITY({Period}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/convexity/Convexity.Quantower.cs";

    public ConvexityIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "CONVEXITY - Beta Convexity";
        Description = "Measures asymmetry between upside and downside beta relative to a market benchmark.";

        _convexitySeries = new LineSeries("Convexity", Color.FromArgb(128, 128, 255), 2, LineStyle.Solid);
        _betaStdSeries = new LineSeries("Beta", Color.FromArgb(255, 255, 128), 1, LineStyle.Solid);
        _betaUpSeries = new LineSeries("Beta+", Color.FromArgb(128, 255, 128), 1, LineStyle.Dash);
        _betaDownSeries = new LineSeries("Beta-", Color.FromArgb(255, 128, 128), 1, LineStyle.Dash);
        _ratioSeries = new LineSeries("Ratio", Color.FromArgb(255, 165, 0), 1, LineStyle.Dot);

        AddLineSeries(_convexitySeries);
        AddLineSeries(_betaStdSeries);
        AddLineSeries(_betaUpSeries);
        AddLineSeries(_betaDownSeries);
        AddLineSeries(_ratioSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _convexity = new Convexity(Period);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        // Use selected source as asset, and Open as market proxy
        double assetPrice = _priceSelector(item);
        double marketPrice = item[PriceType.Open];

        _convexity.Update(
            new TValue(item.TimeLeft.Ticks, assetPrice),
            new TValue(item.TimeLeft.Ticks, marketPrice),
            args.IsNewBar());

        bool isHot = _convexity.IsHot;
        _convexitySeries.SetValue(_convexity.ConvexityValue, isHot, ShowColdValues);
        _betaStdSeries.SetValue(_convexity.BetaStd, isHot, ShowColdValues);
        _betaUpSeries.SetValue(_convexity.BetaUp, isHot, ShowColdValues);
        _betaDownSeries.SetValue(_convexity.BetaDown, isHot, ShowColdValues);
        _ratioSeries.SetValue(_convexity.Ratio, isHot, ShowColdValues);
    }
}
