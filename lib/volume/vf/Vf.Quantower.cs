using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 1000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vf _vf = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => Period;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => Period;

    public override string ShortName => $"VF({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/vf/Vf.Quantower.cs";

    public VfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VF - Volume Force";
        Description = "Measures the force of volume behind price movements using EMA smoothing with warmup compensation.";

        _series = new LineSeries(name: "VF", color: Color.Magenta, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vf = new Vf(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vf.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _vf.IsHot, ShowColdValues);
    }
}