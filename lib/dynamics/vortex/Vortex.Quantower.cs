using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VortexIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vortex _vortex = null!;
    private readonly LineSeries _viPlusSeries;
    private readonly LineSeries _viMinusSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Vortex {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/vortex/Vortex.Quantower.cs";

    public VortexIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Vortex";
        Description = "Vortex Indicator identifies trend direction using VI+ and VI-";

        _viPlusSeries = new LineSeries(name: "VI+", color: Color.Green, width: 2, style: LineStyle.Solid);
        _viMinusSeries = new LineSeries(name: "VI-", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_viPlusSeries);
        AddLineSeries(_viMinusSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vortex = new Vortex(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _vortex.Update(this.GetInputBar(args), args.IsNewBar());

        _viPlusSeries.SetValue(_vortex.ViPlus.Value, _vortex.IsHot, ShowColdValues);
        _viMinusSeries.SetValue(_vortex.ViMinus.Value, _vortex.IsHot, ShowColdValues);
    }
}
