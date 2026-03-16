using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VwadIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vwad _vwad = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => Period;

    public override string ShortName => $"VWAD({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/vwad/Vwad.Quantower.cs";

    public VwadIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VWAD - Volume Weighted Accumulation/Distribution";
        Description = "Volume Weighted Accumulation/Distribution enhances AD by weighting each bar's contribution based on relative volume";

        _series = new LineSeries(name: "VWAD", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vwad = new Vwad(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vwad.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _vwad.IsHot, ShowColdValues);
    }
}