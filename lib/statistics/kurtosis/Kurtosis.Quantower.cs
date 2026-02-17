using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class KurtosisIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 4, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population Kurtosis", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Kurtosis _kurtosis = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Kurtosis {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/kurtosis/Kurtosis.Quantower.cs";

    public KurtosisIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Kurtosis - Excess Kurtosis";
        Description = "Measures the tailedness of the probability distribution. Positive = fat tails, Negative = thin tails.";

        _series = new LineSeries(name: "Kurtosis", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _kurtosis = new Kurtosis(Period, IsPopulation);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        TValue result = _kurtosis.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _kurtosis.IsHot, ShowColdValues);
    }
}
