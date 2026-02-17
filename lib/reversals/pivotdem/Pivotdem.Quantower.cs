using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PivotdemIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pivotdem _indicator = null!;
    private readonly LineSeries _ppSeries;
    private readonly LineSeries _r1Series;
    private readonly LineSeries _s1Series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "PIVOTDEM";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/pivotdem/Pivotdem.cs";

    public PivotdemIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "PIVOTDEM - DeMark Pivot Points";
        Description = "DeMark pivot points: 3 support/resistance levels (PP, R1, S1) with conditional logic based on Open vs Close.";

        _ppSeries = new LineSeries(name: "PP", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _r1Series = new LineSeries(name: "R1", color: Color.FromArgb(255, 128, 128), width: 1, style: LineStyle.Solid);
        _s1Series = new LineSeries(name: "S1", color: Color.FromArgb(128, 255, 128), width: 1, style: LineStyle.Solid);

        AddLineSeries(_ppSeries);
        AddLineSeries(_r1Series);
        AddLineSeries(_s1Series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Pivotdem();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _ppSeries.SetValue(_indicator.PP, _indicator.IsHot, ShowColdValues);
        _r1Series.SetValue(_indicator.R1, _indicator.IsHot, ShowColdValues);
        _s1Series.SetValue(_indicator.S1, _indicator.IsHot, ShowColdValues);
    }
}
