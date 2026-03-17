using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BwMfiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private BwMfi _bwMfi = null!;
    private readonly LineSeries _mfiLine;
    private readonly LineSeries _zoneLine;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "BW_MFI";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/bw_mfi/BwMfi.Quantower.cs";

    public BwMfiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "BW_MFI - Bill Williams Market Facilitation Index";
        Description = "Bill Williams' MFI with 4-zone classification. Zone 1 (Green): trend continuation. Zone 2 (Fade): fading. Zone 3 (Fake): unsupported. Zone 4 (Squat): breakout imminent.";

        _mfiLine = new LineSeries("BW_MFI", Color.Cyan, 2, LineStyle.Histogramm);
        _zoneLine = new LineSeries("Zone", Color.Gray, 1, LineStyle.Solid) { Visible = false };
        AddLineSeries(_mfiLine);
        AddLineSeries(_zoneLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _bwMfi = new BwMfi();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _bwMfi.Update(this.GetInputBar(args), args.IsNewBar());

        // Zone-based coloring
        Color barColor = _bwMfi.Zone switch
        {
            1 => Color.Green,       // Green zone
            2 => Color.SaddleBrown, // Fade zone
            3 => Color.Blue,        // Fake zone
            4 => Color.Fuchsia,     // Squat zone
            _ => Color.Gray         // First bar
        };
        _mfiLine.SetValue(_bwMfi.Last.Value, _bwMfi.IsHot, ShowColdValues);
        _mfiLine.SetMarker(0, barColor);
        _zoneLine.SetValue(_bwMfi.Zone, _bwMfi.IsHot, ShowColdValues);
    }
}
