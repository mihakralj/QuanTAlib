using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BetaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Asset Source", sortIndex: 2)]
    public SourceType AssetSource { get; set; } = SourceType.Close;

    [InputParameter("Market Source", sortIndex: 3)]
    public SourceType MarketSource { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Beta _beta = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _assetSelector = null!;
    private Func<IHistoryItem, double> _marketSelector = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Beta({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/beta/Beta.Quantower.cs";

    public BetaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Beta Coefficient";
        Description = "Measures the volatility of an asset in relation to the overall market.";

        _series = new LineSeries(name: "Beta", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _beta = new Beta(Period);
        _assetSelector = AssetSource.GetPriceSelector();
        _marketSelector = MarketSource.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double assetVal = _assetSelector(item);
        double marketVal = _marketSelector(item);
        var time = this.HistoricalData.Time();

        var assetInput = new TValue(time, assetVal);
        var marketInput = new TValue(time, marketVal);

        TValue result = _beta.Update(assetInput, marketInput, args.IsNewBar());

        _series.SetValue(result.Value, _beta.IsHot, ShowColdValues);
    }
}
