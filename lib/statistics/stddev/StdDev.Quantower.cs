using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class StdDevIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population StdDev", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private StdDev? _stdDev;
    private readonly LineSeries? _series;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"StdDev {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/stddev/StdDev.Quantower.cs";

    public StdDevIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "StdDev - Standard Deviation";
        Description = "Measures the amount of variation or dispersion of a set of values";

        _series = new(name: "StdDev", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _stdDev = new StdDev(Period, IsPopulation);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector!(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        TValue result = _stdDev!.Update(input, args.IsNewBar());

        _series!.SetValue(result.Value, _stdDev.IsHot, ShowColdValues);
    }
}
