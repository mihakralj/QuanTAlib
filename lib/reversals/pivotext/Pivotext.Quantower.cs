using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PivotextIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pivotext _indicator = null!;
    private readonly LineSeries _ppSeries;
    private readonly LineSeries _r1Series;
    private readonly LineSeries _r2Series;
    private readonly LineSeries _r3Series;
    private readonly LineSeries _r4Series;
    private readonly LineSeries _r5Series;
    private readonly LineSeries _s1Series;
    private readonly LineSeries _s2Series;
    private readonly LineSeries _s3Series;
    private readonly LineSeries _s4Series;
    private readonly LineSeries _s5Series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "PIVOTEXT";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/pivotext/Pivotext.cs";

    public PivotextIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "PIVOTEXT - Extended Traditional Pivot Points";
        Description = "Extended traditional pivot points: 11 support/resistance levels (PP, R1-R5, S1-S5) derived from previous bar's HLC.";

        _ppSeries = new LineSeries(name: "PP", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _r1Series = new LineSeries(name: "R1", color: Color.FromArgb(255, 180, 180), width: 1, style: LineStyle.Solid);
        _r2Series = new LineSeries(name: "R2", color: Color.FromArgb(255, 140, 140), width: 1, style: LineStyle.Solid);
        _r3Series = new LineSeries(name: "R3", color: Color.FromArgb(255, 100, 100), width: 1, style: LineStyle.Solid);
        _r4Series = new LineSeries(name: "R4", color: Color.FromArgb(255, 60, 60), width: 1, style: LineStyle.Dash);
        _r5Series = new LineSeries(name: "R5", color: Color.Red, width: 1, style: LineStyle.Dash);
        _s1Series = new LineSeries(name: "S1", color: Color.FromArgb(180, 255, 180), width: 1, style: LineStyle.Solid);
        _s2Series = new LineSeries(name: "S2", color: Color.FromArgb(140, 255, 140), width: 1, style: LineStyle.Solid);
        _s3Series = new LineSeries(name: "S3", color: Color.FromArgb(100, 255, 100), width: 1, style: LineStyle.Solid);
        _s4Series = new LineSeries(name: "S4", color: Color.FromArgb(60, 255, 60), width: 1, style: LineStyle.Dash);
        _s5Series = new LineSeries(name: "S5", color: Color.Green, width: 1, style: LineStyle.Dash);

        AddLineSeries(_ppSeries);
        AddLineSeries(_r1Series);
        AddLineSeries(_r2Series);
        AddLineSeries(_r3Series);
        AddLineSeries(_r4Series);
        AddLineSeries(_r5Series);
        AddLineSeries(_s1Series);
        AddLineSeries(_s2Series);
        AddLineSeries(_s3Series);
        AddLineSeries(_s4Series);
        AddLineSeries(_s5Series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Pivotext();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _ppSeries.SetValue(_indicator.PP, _indicator.IsHot, ShowColdValues);
        _r1Series.SetValue(_indicator.R1, _indicator.IsHot, ShowColdValues);
        _r2Series.SetValue(_indicator.R2, _indicator.IsHot, ShowColdValues);
        _r3Series.SetValue(_indicator.R3, _indicator.IsHot, ShowColdValues);
        _r4Series.SetValue(_indicator.R4, _indicator.IsHot, ShowColdValues);
        _r5Series.SetValue(_indicator.R5, _indicator.IsHot, ShowColdValues);
        _s1Series.SetValue(_indicator.S1, _indicator.IsHot, ShowColdValues);
        _s2Series.SetValue(_indicator.S2, _indicator.IsHot, ShowColdValues);
        _s3Series.SetValue(_indicator.S3, _indicator.IsHot, ShowColdValues);
        _s4Series.SetValue(_indicator.S4, _indicator.IsHot, ShowColdValues);
        _s5Series.SetValue(_indicator.S5, _indicator.IsHot, ShowColdValues);
    }
}
