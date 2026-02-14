using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EntropyIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Entropy _entropy = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Entropy {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/entropy/Entropy.Quantower.cs";

    public EntropyIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Entropy - Shannon Entropy";
        Description = "Measures the randomness/predictability of price data using normalized Shannon entropy";

        _series = new LineSeries(name: "Entropy", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _entropy = new Entropy(Period);
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
        TValue result = _entropy.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _entropy.IsHot, ShowColdValues);
    }
}
