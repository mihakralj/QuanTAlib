using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HurstIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 20, 2000, 1, 0)]
    public int Period { get; set; } = 100;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hurst _hurst = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _halfLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Hurst {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/hurst/Hurst.Quantower.cs";

    public HurstIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Hurst - Hurst Exponent";
        Description = "Measures long-range dependence using Rescaled Range (R/S) analysis. H > 0.5 = trending, H < 0.5 = mean-reverting, H ≈ 0.5 = random walk";

        _series = new LineSeries(name: "Hurst", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        _halfLine = new LineSeries(name: "0.5", color: Color.Gray, width: 1, style: LineStyle.Dash);
        AddLineSeries(_series);
        AddLineSeries(_halfLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _hurst = new Hurst(Period);
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
        TValue result = _hurst.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _hurst.IsHot, ShowColdValues);
        _halfLine.SetValue(0.5);
    }
}
