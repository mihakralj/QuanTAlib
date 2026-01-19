using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VarianceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population Variance", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Variance _variance = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Variance {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/variance/Variance.Quantower.cs";

    public VarianceIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Variance - Rolling Variance";
        Description = "Measures the dispersion of a set of data points around their mean";

        _series = new LineSeries(name: "Variance", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _variance = new Variance(Period, IsPopulation);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
            return;

        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        TValue result = _variance.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _variance.IsHot, ShowColdValues);
    }
}
