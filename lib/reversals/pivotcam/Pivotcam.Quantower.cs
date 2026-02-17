using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PivotcamIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pivotcam _indicator = null!;
    private readonly LineSeries _ppSeries;
    private readonly LineSeries _r1Series;
    private readonly LineSeries _r2Series;
    private readonly LineSeries _r3Series;
    private readonly LineSeries _r4Series;
    private readonly LineSeries _s1Series;
    private readonly LineSeries _s2Series;
    private readonly LineSeries _s3Series;
    private readonly LineSeries _s4Series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "PIVOTCAM";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/pivotcam/Pivotcam.cs";

    public PivotcamIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "PIVOTCAM - Camarilla Pivot Points";
        Description = "Camarilla pivot points: 9 support/resistance levels (PP, R1-R4, S1-S4) derived from previous bar's HLC.";

        _ppSeries = new LineSeries(name: "PP", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _r1Series = new LineSeries(name: "R1", color: Color.FromArgb(255, 160, 160), width: 1, style: LineStyle.Solid);
        _r2Series = new LineSeries(name: "R2", color: Color.FromArgb(255, 128, 128), width: 1, style: LineStyle.Solid);
        _r3Series = new LineSeries(name: "R3", color: Color.FromArgb(255, 80, 80), width: 1, style: LineStyle.Solid);
        _r4Series = new LineSeries(name: "R4", color: Color.Red, width: 1, style: LineStyle.Dash);
        _s1Series = new LineSeries(name: "S1", color: Color.FromArgb(160, 255, 160), width: 1, style: LineStyle.Solid);
        _s2Series = new LineSeries(name: "S2", color: Color.FromArgb(128, 255, 128), width: 1, style: LineStyle.Solid);
        _s3Series = new LineSeries(name: "S3", color: Color.FromArgb(80, 255, 80), width: 1, style: LineStyle.Solid);
        _s4Series = new LineSeries(name: "S4", color: Color.Green, width: 1, style: LineStyle.Dash);

        AddLineSeries(_ppSeries);
        AddLineSeries(_r1Series);
        AddLineSeries(_r2Series);
        AddLineSeries(_r3Series);
        AddLineSeries(_r4Series);
        AddLineSeries(_s1Series);
        AddLineSeries(_s2Series);
        AddLineSeries(_s3Series);
        AddLineSeries(_s4Series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Pivotcam();
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
        _s1Series.SetValue(_indicator.S1, _indicator.IsHot, ShowColdValues);
        _s2Series.SetValue(_indicator.S2, _indicator.IsHot, ShowColdValues);
        _s3Series.SetValue(_indicator.S3, _indicator.IsHot, ShowColdValues);
        _s4Series.SetValue(_indicator.S4, _indicator.IsHot, ShowColdValues);
    }
}
