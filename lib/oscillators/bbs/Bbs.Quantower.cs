using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// BBS: Bollinger Band Squeeze - Quantower Indicator Adapter
/// Detects when Bollinger Bands contract inside Keltner Channels.
/// Outputs bandwidth histogram with squeeze dots at zero line.
/// </summary>
[SkipLocalsInit]
public sealed class BbsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("BB Period", sortIndex: 1, 1, 500, 1, 0)]
    public int BbPeriod { get; set; } = 20;

    [InputParameter("BB Multiplier", sortIndex: 2, 0.1, 10.0, 0.1, 1)]
    public double BbMult { get; set; } = 2.0;

    [InputParameter("KC Period", sortIndex: 3, 1, 500, 1, 0)]
    public int KcPeriod { get; set; } = 20;

    [InputParameter("KC Multiplier", sortIndex: 4, 0.1, 10.0, 0.1, 1)]
    public double KcMult { get; set; } = 1.5;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    private Bbs _bbs = null!;
    private readonly LineSeries _bandwidthSeries;
    private readonly LineSeries _squeezeSeries;

    public override string ShortName => $"BBS({BbPeriod},{BbMult:F1},{KcPeriod},{KcMult:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/bbs/Bbs.Quantower.cs";

    public BbsIndicator()
    {
        Name = "BBS - Bollinger Band Squeeze";
        Description = "Detects when Bollinger Bands contract inside Keltner Channels, indicating consolidation before breakout";
        SeparateWindow = true;
        OnBackGround = true;

        _bandwidthSeries = new LineSeries("Bandwidth", Color.Cyan, 2, LineStyle.Histogramm);
        _squeezeSeries = new LineSeries("Squeeze", Color.Red, 4, LineStyle.Dot);

        AddLineSeries(_bandwidthSeries);
        AddLineSeries(_squeezeSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _bbs = new Bbs(BbPeriod, BbMult, KcPeriod, KcMult);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        bool isNew = args.IsNewBar();

        TValue result = _bbs.Update(bar, isNew);

        if (!ShowColdValues && !_bbs.IsHot)
        {
            return;
        }

        int offset = args.Reason == UpdateReason.HistoricalBar ? 0 : -1;

        // Set bandwidth histogram
        _bandwidthSeries.SetValue(result.Value, offset);

        // Set squeeze indicator dot at zero line
        _squeezeSeries.SetValue(0, offset);

        // Red dot = squeeze on, Green dot = squeeze off
        Color squeezeColor = _bbs.SqueezeOn ? Color.Red : Color.Green;
        _squeezeSeries.SetMarker(offset, squeezeColor);
    }
}
