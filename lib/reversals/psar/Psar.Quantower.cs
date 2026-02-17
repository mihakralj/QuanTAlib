using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PsarIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Start AF", sortIndex: 0, 0.001, 1.0, 0.001, 3)]
    public double AfStart { get; set; } = 0.02;

    [InputParameter("AF Increment", sortIndex: 1, 0.001, 1.0, 0.001, 3)]
    public double AfIncrement { get; set; } = 0.02;

    [InputParameter("Max AF", sortIndex: 2, 0.001, 1.0, 0.01, 2)]
    public double AfMax { get; set; } = 0.20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Psar _indicator = null!;
    private readonly LineSeries _sarSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PSAR({AfStart:F2},{AfIncrement:F2},{AfMax:F2})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/psar/Psar.cs";

    public PsarIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "PSAR - Parabolic Stop And Reverse";
        Description = "Trend-following trailing stop indicator. SAR accelerates toward price as trend progresses, flipping on reversal.";

        _sarSeries = new LineSeries(name: "SAR", color: Color.DodgerBlue, width: 2, style: LineStyle.Dot);

        AddLineSeries(_sarSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Psar(AfStart, AfIncrement, AfMax);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _sarSeries.SetValue(_indicator.Sar, _indicator.IsHot, ShowColdValues);
    }
}
