using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SqueezeProIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 500, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("BB Multiplier", sortIndex: 2, 0.001, 10.0, 0.1, 1)]
    public double BbMult { get; set; } = 2.0;

    [InputParameter("KC Wide Multiplier", sortIndex: 3, 0.001, 10.0, 0.1, 1)]
    public double KcMultWide { get; set; } = 2.0;

    [InputParameter("KC Normal Multiplier", sortIndex: 4, 0.001, 10.0, 0.1, 1)]
    public double KcMultNormal { get; set; } = 1.5;

    [InputParameter("KC Narrow Multiplier", sortIndex: 5, 0.001, 10.0, 0.1, 1)]
    public double KcMultNarrow { get; set; } = 1.0;

    [InputParameter("Momentum Length", sortIndex: 6, 1, 500, 1, 0)]
    public int MomLength { get; set; } = 12;

    [InputParameter("Momentum Smooth", sortIndex: 7, 1, 500, 1, 0)]
    public int MomSmooth { get; set; } = 6;

    [InputParameter("Use SMA (unchecked = EMA)", sortIndex: 8)]
    public bool UseSma { get; set; } = true;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private SqueezePro _squeezePro = null!;
    private readonly LineSeries _momentumSeries;
    private readonly LineSeries _squeezeSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SQZ_PRO {Period},{BbMult},{KcMultWide},{KcMultNormal},{KcMultNarrow}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/squeeze_pro/SqueezePro.cs";

    public SqueezeProIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SQUEEZE_PRO";
        Description = "Squeeze Pro: Multi-level BB vs KC squeeze detection with MOM-smoothed momentum";

        _momentumSeries = new LineSeries(name: "Momentum", color: Color.Lime, width: 2, style: LineStyle.Histogramm);
        _squeezeSeries = new LineSeries(name: "SqueezeLevel", color: Color.Red, width: 4, style: LineStyle.Dot);

        AddLineSeries(_momentumSeries);
        AddLineSeries(_squeezeSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _squeezePro = new SqueezePro(Period, BbMult, KcMultWide, KcMultNormal, KcMultNarrow,
            MomLength, MomSmooth, UseSma);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _squeezePro.Update(this.GetInputBar(args), args.IsNewBar());

        _momentumSeries.SetValue(_squeezePro.Momentum, _squeezePro.IsHot, ShowColdValues);

        // Plot squeeze level dot at 0 (colored by level), NaN when off
        double sqDot = _squeezePro.SqueezeLevel > 0 ? 0.0 : double.NaN;
        _squeezeSeries.SetValue(sqDot, _squeezePro.IsHot, ShowColdValues);

        // Color squeeze dot: Red=narrow(3), Orange=normal(2), Yellow=wide(1)
        if (_squeezePro.SqueezeLevel == 3)
        {
            _squeezeSeries.Color = Color.Red;
        }
        else if (_squeezePro.SqueezeLevel == 2)
        {
            _squeezeSeries.Color = Color.Orange;
        }
        else if (_squeezePro.SqueezeLevel == 1)
        {
            _squeezeSeries.Color = Color.Yellow;
        }
    }
}
