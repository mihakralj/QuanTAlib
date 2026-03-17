using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AtrstopIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, 2, 500, 1, 0)]
    public int Period { get; set; } = 21;

    [InputParameter("Multiplier", sortIndex: 1, 0.1, 20.0, 0.1, 1)]
    public double Multiplier { get; set; } = 3.0;

    [InputParameter("Use High/Low", sortIndex: 2)]
    public bool UseHighLow { get; set; }

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Atrstop _indicator = null!;
    private readonly LineSeries _stopSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ATRSTOP({Period},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/atrstop/Atrstop.cs";

    public AtrstopIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "ATRSTOP - ATR Trailing Stop";
        Description = "Dynamic trailing stop using ATR multiplier with band ratcheting.";

        _stopSeries = new LineSeries(name: "ATRSTOP", color: Color.Crimson, width: 2, style: LineStyle.Dot);

        AddLineSeries(_stopSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Atrstop(Period, Multiplier, UseHighLow);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _stopSeries.SetValue(_indicator.StopValue, _indicator.IsHot, ShowColdValues);
    }
}
