using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for VWAP (Volume Weighted Average Price).
/// </summary>
[SkipLocalsInit]
public sealed class VwapIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period (0 = no reset)", sortIndex: 10, 0, 10000, 1, 0)]
    public int Period { get; set; }

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vwap _vwap = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period > 0 ? Period : 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => Period > 0 ? $"VWAP({Period})" : "VWAP";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/vwap/Vwap.Quantower.cs";

    public VwapIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "VWAP - Volume Weighted Average Price";
        Description = "Volume Weighted Average Price calculates the average price weighted by volume";

        _series = new LineSeries(name: "VWAP", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vwap = new Vwap(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vwap.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _vwap.IsHot, ShowColdValues);
    }
}