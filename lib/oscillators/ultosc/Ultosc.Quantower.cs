using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class UltoscIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period 1 (Short)", sortIndex: 1, 1, 500, 1, 0)]
    public int Period1 { get; set; } = 7;

    [InputParameter("Period 2 (Medium)", sortIndex: 2, 1, 500, 1, 0)]
    public int Period2 { get; set; } = 14;

    [InputParameter("Period 3 (Long)", sortIndex: 3, 1, 500, 1, 0)]
    public int Period3 { get; set; } = 28;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ultosc _ultosc = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ULTOSC {Period1},{Period2},{Period3}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/ultosc/Ultosc.cs";

    public UltoscIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ULTOSC - Ultimate Oscillator";
        Description = "Ultimate Oscillator by Larry Williams using weighted averages of three time periods";

        _series = new LineSeries(name: "ULTOSC", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ultosc = new Ultosc(Period1, Period2, Period3);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _ultosc.Update(this.GetInputBar(args), args.IsNewBar());

        _series.SetValue(_ultosc.Last.Value, _ultosc.IsHot, ShowColdValues);
    }
}
