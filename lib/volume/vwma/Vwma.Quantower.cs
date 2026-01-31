using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for VWMA (Volume Weighted Moving Average).
/// </summary>
[SkipLocalsInit]
public sealed class VwmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 10000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vwma _vwma = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VWMA({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/vwma/Vwma.Quantower.cs";

    public VwmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "VWMA - Volume Weighted Moving Average";
        Description = "Volume Weighted Moving Average calculates a moving average weighted by volume over a specified period";

        _series = new LineSeries(name: "VWMA", color: Color.Cyan, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vwma = new Vwma(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vwma.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _vwma.IsHot, ShowColdValues);
    }
}