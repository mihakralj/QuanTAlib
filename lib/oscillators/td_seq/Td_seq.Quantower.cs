using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TdSeqIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Compare Period", sortIndex: 1, 1, 100, 1, 0)]
    public int ComparePeriod { get; set; } = 4;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private TdSeq _tdSeq = null!;
    private readonly LineSeries _setupLine;
    private readonly LineSeries _countdownLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"TD_SEQ ({ComparePeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/td_seq/Td_seq.Quantower.cs";

    public TdSeqIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "TD_SEQ - TD Sequential";
        Description = "Tom DeMark's exhaustion counting system: Setup (±1 to ±9) and Countdown (±1 to ±13) phases detecting trend reversals.";

        _setupLine = new LineSeries("Setup", Color.Yellow, 2, LineStyle.Solid);
        _countdownLine = new LineSeries("Countdown", Color.Cyan, 1, LineStyle.Solid);

        AddLineSeries(_setupLine);
        AddLineSeries(_countdownLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _tdSeq = new TdSeq(ComparePeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _tdSeq.Update(this.GetInputBar(args), args.IsNewBar());

        _setupLine.SetValue(_tdSeq.Setup, _tdSeq.IsHot, ShowColdValues);
        _countdownLine.SetValue(_tdSeq.Countdown, _tdSeq.IsHot, ShowColdValues);
    }
}
