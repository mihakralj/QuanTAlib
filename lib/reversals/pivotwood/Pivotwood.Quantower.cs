using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PivotwoodIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pivotwood _indicator = null!;
    private readonly LineSeries _ppSeries;
    private readonly LineSeries _r1Series;
    private readonly LineSeries _r2Series;
    private readonly LineSeries _r3Series;
    private readonly LineSeries _s1Series;
    private readonly LineSeries _s2Series;
    private readonly LineSeries _s3Series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "PIVOTWOOD";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/pivotwood/Pivotwood.cs";

    public PivotwoodIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "PIVOTWOOD - Woodie's Pivot Points";
        Description = "Woodie's pivot points: 7 support/resistance levels (PP, R1-R3, S1-S3) with close weighted twice in the PP calculation, biasing toward recent price action.";

        _ppSeries = new LineSeries(name: "PP", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _r1Series = new LineSeries(name: "R1", color: Color.FromArgb(255, 200, 200), width: 1, style: LineStyle.Solid);
        _r2Series = new LineSeries(name: "R2", color: Color.FromArgb(255, 150, 150), width: 1, style: LineStyle.Solid);
        _r3Series = new LineSeries(name: "R3", color: Color.FromArgb(255, 100, 100), width: 1, style: LineStyle.Dash);
        _s1Series = new LineSeries(name: "S1", color: Color.FromArgb(200, 255, 200), width: 1, style: LineStyle.Solid);
        _s2Series = new LineSeries(name: "S2", color: Color.FromArgb(150, 255, 150), width: 1, style: LineStyle.Solid);
        _s3Series = new LineSeries(name: "S3", color: Color.FromArgb(100, 255, 100), width: 1, style: LineStyle.Dash);

        AddLineSeries(_ppSeries);
        AddLineSeries(_r1Series);
        AddLineSeries(_r2Series);
        AddLineSeries(_r3Series);
        AddLineSeries(_s1Series);
        AddLineSeries(_s2Series);
        AddLineSeries(_s3Series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Pivotwood();
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
        _s1Series.SetValue(_indicator.S1, _indicator.IsHot, ShowColdValues);
        _s2Series.SetValue(_indicator.S2, _indicator.IsHot, ShowColdValues);
        _s3Series.SetValue(_indicator.S3, _indicator.IsHot, ShowColdValues);
    }
}
