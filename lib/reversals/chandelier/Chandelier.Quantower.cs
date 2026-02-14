using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ChandelierIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, 1, 500, 1, 0)]
    public int Period { get; set; } = 22;

    [InputParameter("Multiplier", sortIndex: 1, 0.1, 20.0, 0.1, 1)]
    public double Multiplier { get; set; } = 3.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Chandelier _indicator = null!;
    private readonly LineSeries _exitLongSeries;
    private readonly LineSeries _exitShortSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CHANDELIER({Period},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/chandelier/Chandelier.cs";

    public ChandelierIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "CHANDELIER - Chandelier Exit";
        Description = "ATR-based trailing exit indicator. Two overlay lines: ExitLong (green) for long position exits, ExitShort (red) for short position exits.";

        _exitLongSeries = new LineSeries(name: "Exit Long", color: Color.Green, width: 2, style: LineStyle.Solid);
        _exitShortSeries = new LineSeries(name: "Exit Short", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_exitLongSeries);
        AddLineSeries(_exitShortSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Chandelier(Period, Multiplier);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _exitLongSeries.SetValue(_indicator.ExitLong, _indicator.IsHot, ShowColdValues);
        _exitShortSeries.SetValue(_indicator.ExitShort, _indicator.IsHot, ShowColdValues);
    }
}
