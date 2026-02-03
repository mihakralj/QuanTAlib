using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 200, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vr _vr = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VR({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/vr/Vr.Quantower.cs";

    public VrIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VR - Volatility Ratio";
        Description = "Volatility Ratio measures current True Range relative to Average True Range, identifying potential breakout conditions when the ratio exceeds threshold values";

        _series = new LineSeries(name: "VR", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _vr = new Vr(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vr.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _vr.IsHot, ShowColdValues);
    }
}