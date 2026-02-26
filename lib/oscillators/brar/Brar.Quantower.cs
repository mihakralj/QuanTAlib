using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BrarIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 5000, 1, 0)]
    public int Period { get; set; } = 26;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Brar _brar = null!;
    private readonly LineSeries _brLine;
    private readonly LineSeries _arLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BRAR ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/brar/Brar.Quantower.cs";

    public BrarIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "BRAR - Bull-Bear Power Ratio";
        Description = "Dual-output Japanese sentiment oscillator: BR (buying ratio vs previous close) and AR (atmosphere ratio vs open). Equilibrium = 100.";

        _brLine = new LineSeries("BR", Color.Cyan, 2, LineStyle.Solid);
        _arLine = new LineSeries("AR", Color.Yellow, 2, LineStyle.Solid);

        AddLineSeries(_brLine);
        AddLineSeries(_arLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _brar = new Brar(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _brar.Update(this.GetInputBar(args), args.IsNewBar());

        _brLine.SetValue(_brar.Br, _brar.IsHot, ShowColdValues);
        _arLine.SetValue(_brar.Ar, _brar.IsHot, ShowColdValues);
    }
}
