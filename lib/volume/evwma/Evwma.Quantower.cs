using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for EVWMA (Elastic Volume Weighted Moving Average).
/// </summary>
[SkipLocalsInit]
public sealed class EvwmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 10000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Evwma _evwma = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"EVWMA({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/evwma/Evwma.Quantower.cs";

    public EvwmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "EVWMA - Elastic Volume Weighted Moving Average";
        Description = "Elastic Volume Weighted Moving Average weights each bar by its volume relative to a rolling volume sum over a specified period";

        _series = new LineSeries(name: "EVWMA", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _evwma = new Evwma(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _evwma.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _evwma.IsHot, ShowColdValues);
    }
}
