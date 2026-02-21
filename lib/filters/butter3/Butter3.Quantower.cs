using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class Butter3Indicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Butter3 _ma = null!;
    protected LineSeries _series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BUTTER3 {Period}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/filters/butter3/Butter3.Quantower.cs";

    public Butter3Indicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "BUTTER3 - Ehlers 3-Pole Butterworth Filter";
        Description = "Ehlers 3-Pole Butterworth Filter: 3rd-order low-pass filter with steeper rolloff than the 2-pole variant.";
        _series = new LineSeries(name: $"BUTTER3 {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ma = new Butter3(Period);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        TValue result = _ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());

        _series.SetValue(result.Value, _ma.IsHot, ShowColdValues);
    }
}
