using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class JbIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 3, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Jb _jb = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _crit10;
    private readonly LineSeries _crit05;
    private readonly LineSeries _crit01;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"JB {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/jb/Jb.Quantower.cs";

    public JbIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "JB - Jarque-Bera Test";
        Description = "Normality test using skewness and kurtosis. Large values reject normality.";

        _series = new LineSeries(name: "JB", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        _crit10 = new LineSeries(name: "10%", color: Color.Gray, width: 1, style: LineStyle.Dash);
        _crit05 = new LineSeries(name: "5%", color: Color.Orange, width: 1, style: LineStyle.Dash);
        _crit01 = new LineSeries(name: "1%", color: Color.Red, width: 1, style: LineStyle.Solid);
        AddLineSeries(_series);
        AddLineSeries(_crit10);
        AddLineSeries(_crit05);
        AddLineSeries(_crit01);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _jb = new Jb(Period);
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
        TValue result = _jb.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _jb.IsHot, ShowColdValues);
        _crit10.SetValue(4.605);
        _crit05.SetValue(5.991);
        _crit01.SetValue(9.210);
    }
}
