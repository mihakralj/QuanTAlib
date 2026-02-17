using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class FractalsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Fractals _indicator = null!;
    private readonly LineSeries _upFractalSeries;
    private readonly LineSeries _downFractalSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "FRACTALS";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/fractals/Fractals.cs";

    public FractalsIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "FRACTALS - Williams Fractals";
        Description = "Five-bar pattern identifying local peaks (up fractals / resistance) and troughs (down fractals / support).";

        _upFractalSeries = new LineSeries(name: "Up Fractal", color: Color.Red, width: 2, style: LineStyle.Dot);
        _downFractalSeries = new LineSeries(name: "Down Fractal", color: Color.Green, width: 2, style: LineStyle.Dot);

        AddLineSeries(_upFractalSeries);
        AddLineSeries(_downFractalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Fractals();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _upFractalSeries.SetValue(_indicator.UpFractal, _indicator.IsHot, ShowColdValues);
        _downFractalSeries.SetValue(_indicator.DownFractal, _indicator.IsHot, ShowColdValues);
    }
}
