using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TtmScalperIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Use closes", sortIndex: 10)]
    public bool UseCloses { get; set; }

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private TtmScalper _indicator = null!;
    private readonly LineSeries _pivotHighSeries;
    private readonly LineSeries _pivotLowSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "TTM_SCALPER";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/ttm_scalper/TtmScalper.cs";

    public TtmScalperIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "TTM_SCALPER - TTM Scalper Alert";
        Description = "Three-bar pivot pattern detecting potential reversal points for scalping entries.";

        _pivotHighSeries = new LineSeries(name: "Pivot High", color: Color.Red, width: 2, style: LineStyle.Dot);
        _pivotLowSeries = new LineSeries(name: "Pivot Low", color: Color.Green, width: 2, style: LineStyle.Dot);

        AddLineSeries(_pivotHighSeries);
        AddLineSeries(_pivotLowSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new TtmScalper(UseCloses);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _pivotHighSeries.SetValue(_indicator.PivotHigh, _indicator.IsHot, ShowColdValues);
        _pivotLowSeries.SetValue(_indicator.PivotLow, _indicator.IsHot, ShowColdValues);
    }
}
