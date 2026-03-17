using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VstopIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, 2, 500, 1, 0)]
    public int Period { get; set; } = 7;

    [InputParameter("Multiplier", sortIndex: 1, 0.1, 20.0, 0.1, 1)]
    public double Multiplier { get; set; } = 3.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vstop _indicator = null!;
    private readonly LineSeries _sarSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VSTOP({Period},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/vstop/Vstop.cs";

    public VstopIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "VSTOP - Volatility Stop";
        Description = "ATR-based trailing stop. Tracks SIC (Significant Close) and flips on reversal.";

        _sarSeries = new LineSeries(name: "VSTOP", color: Color.OrangeRed, width: 2, style: LineStyle.Dot);

        AddLineSeries(_sarSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Vstop(Period, Multiplier);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _sarSeries.SetValue(_indicator.SarValue, _indicator.IsHot, ShowColdValues);
    }
}
