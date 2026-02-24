using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ha _ha = null!;
    private readonly LineSeries _openSeries;
    private readonly LineSeries _highSeries;
    private readonly LineSeries _lowSeries;
    private readonly LineSeries _closeSeries;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "HA";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/core/ha/Ha.Quantower.cs";

    public HaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "HA - Heikin-Ashi";
        Description = "Transforms standard OHLC bars into smoothed Heikin-Ashi candles that filter noise and clarify trend direction.";

        _openSeries = new LineSeries(name: "HA Open", color: Color.FromArgb(0, 200, 0), width: 2, style: LineStyle.Solid);
        _highSeries = new LineSeries(name: "HA High", color: IndicatorExtensions.Averages, width: 1, style: LineStyle.Solid);
        _lowSeries = new LineSeries(name: "HA Low", color: IndicatorExtensions.Averages, width: 1, style: LineStyle.Solid);
        _closeSeries = new LineSeries(name: "HA Close", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);

        AddLineSeries(_openSeries);
        AddLineSeries(_highSeries);
        AddLineSeries(_lowSeries);
        AddLineSeries(_closeSeries);
    }

    protected override void OnInit()
    {
        _ha = new Ha();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        _ = _ha.UpdateBar(bar, isNew: args.IsNewBar());

        TBar haBar = _ha.LastBar;
        _openSeries.SetValue(haBar.Open, _ha.IsHot, ShowColdValues);
        _highSeries.SetValue(haBar.High, _ha.IsHot, ShowColdValues);
        _lowSeries.SetValue(haBar.Low, _ha.IsHot, ShowColdValues);
        _closeSeries.SetValue(haBar.Close, _ha.IsHot, ShowColdValues);
    }
}
