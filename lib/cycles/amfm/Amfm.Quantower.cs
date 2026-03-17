using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AmfmIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("FM Super Smoother Period", sortIndex: 1, 1, 5000, 1, 0)]
    public int Period { get; set; } = 30;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Amfm _amfm = null!;
    private readonly LineSeries _amLine;
    private readonly LineSeries _fmLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AMFM ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/amfm/Amfm.Quantower.cs";

    public AmfmIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "AMFM - Ehlers AM Detector / FM Demodulator";
        Description = "Decomposes price into amplitude (AM = volatility) and frequency (FM = timing) via DSP demodulation.";

        _amLine = new LineSeries("AM", Color.Orange, 2, LineStyle.Solid);
        _fmLine = new LineSeries("FM", Color.Cyan, 2, LineStyle.Solid);

        AddLineSeries(_amLine);
        AddLineSeries(_fmLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _amfm = new Amfm(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
        {
            return;
        }

        _ = _amfm.Update(this.GetInputBar(args), args.IsNewBar());

        _amLine.SetValue(_amfm.Am, _amfm.IsHot, ShowColdValues);
        _fmLine.SetValue(_amfm.Fm, _amfm.IsHot, ShowColdValues);
    }
}
