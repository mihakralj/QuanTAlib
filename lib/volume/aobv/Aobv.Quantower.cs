using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AobvIndicator : Indicator, IWatchlistIndicator
{
    private const int SlowPeriod = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Aobv _aobv = null!;
    private readonly LineSeries _fastSeries;
    private readonly LineSeries _slowSeries;

#pragma warning disable S2325 // Interface contract cannot be static
    public int MinHistoryDepths => SlowPeriod;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => SlowPeriod;

    public override string ShortName => "AOBV";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/aobv/Aobv.Quantower.cs";

    public AobvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "AOBV - Archer On-Balance Volume";
        Description = "Archer On-Balance Volume applies dual EMA smoothing to OBV for cleaner signals";

        _fastSeries = new LineSeries(name: "Fast", color: Color.Green, width: 2, style: LineStyle.Solid);
        _slowSeries = new LineSeries(name: "Slow", color: Color.Red, width: 2, style: LineStyle.Solid);
        AddLineSeries(_fastSeries);
        AddLineSeries(_slowSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _aobv = new Aobv();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        _ = _aobv.Update(bar, args.IsNewBar());

        _fastSeries.SetValue(_aobv.LastFast.Value, _aobv.IsHot, ShowColdValues);
        _slowSeries.SetValue(_aobv.LastSlow.Value, _aobv.IsHot, ShowColdValues);
    }
}