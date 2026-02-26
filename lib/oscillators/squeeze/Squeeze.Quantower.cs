using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SqueezeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 500, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("BB Multiplier", sortIndex: 2, 0.001, 10.0, 0.1, 1)]
    public double BbMult { get; set; } = 2.0;

    [InputParameter("KC Multiplier", sortIndex: 3, 0.001, 10.0, 0.1, 1)]
    public double KcMult { get; set; } = 1.5;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Squeeze _squeeze = null!;
    private readonly LineSeries _momentumSeries;
    private readonly LineSeries _squeezeSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SQUEEZE {Period},{BbMult},{KcMult}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/squeeze/Squeeze.cs";

    public SqueezeIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SQUEEZE";
        Description = "Squeeze Momentum: BB vs KC squeeze detection with LinReg momentum histogram";

        _momentumSeries = new LineSeries(name: "Momentum", color: Color.Lime, width: 2, style: LineStyle.Histogramm);
        _squeezeSeries = new LineSeries(name: "SqueezeOn", color: Color.Red, width: 4, style: LineStyle.Dot);

        AddLineSeries(_momentumSeries);
        AddLineSeries(_squeezeSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _squeeze = new Squeeze(Period, BbMult, KcMult);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _squeeze.Update(this.GetInputBar(args), args.IsNewBar());

        _momentumSeries.SetValue(_squeeze.Momentum, _squeeze.IsHot, ShowColdValues);

        // Plot squeeze state dot at 0 when squeeze is on, NaN when off
        double sqDot = _squeeze.SqueezeOn ? 0.0 : double.NaN;
        _squeezeSeries.SetValue(sqDot, _squeeze.IsHot, ShowColdValues);
    }
}
