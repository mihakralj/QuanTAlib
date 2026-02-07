using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// TTM Squeeze: Volatility Breakout Indicator - Quantower Indicator Adapter
/// Combines Bollinger Bands and Keltner Channels to identify squeeze conditions.
/// Momentum histogram shows price deviation from donchian midline.
/// </summary>
[SkipLocalsInit]
public sealed class TtmSqueezeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("BB Period", sortIndex: 1, 2, 200, 1, 0)]
    public int BbPeriod { get; set; } = 20;

    [InputParameter("BB Multiplier", sortIndex: 2, 0.1, 10.0, 0.1, 1)]
    public double BbMult { get; set; } = 2.0;

    [InputParameter("KC Period", sortIndex: 3, 1, 200, 1, 0)]
    public int KcPeriod { get; set; } = 20;

    [InputParameter("KC Multiplier", sortIndex: 4, 0.1, 10.0, 0.1, 1)]
    public double KcMult { get; set; } = 1.5;

    [InputParameter("Momentum Period", sortIndex: 5, 2, 200, 1, 0)]
    public int MomPeriod { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    private TtmSqueeze _squeeze = null!;
    private readonly LineSeries _momentumSeries;
    private readonly LineSeries _squeezeOnSeries;

    public override string ShortName => $"TTM_SQZ({BbPeriod},{BbMult:F1},{KcPeriod},{KcMult:F1},{MomPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/ttm_squeeze/TtmSqueeze.Quantower.cs";

    public TtmSqueezeIndicator()
    {
        Name = "TTM Squeeze";
        Description = "John Carter's volatility breakout indicator combining Bollinger Bands and Keltner Channels";
        SeparateWindow = true;
        OnBackGround = true;

        _momentumSeries = new LineSeries("Momentum", Color.Cyan, 2, LineStyle.Histogramm);
        _squeezeOnSeries = new LineSeries("Squeeze", Color.Red, 4, LineStyle.Dot);

        AddLineSeries(_momentumSeries);
        AddLineSeries(_squeezeOnSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _squeeze = new TtmSqueeze(BbPeriod, BbMult, KcPeriod, KcMult, MomPeriod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        bool isNew = args.Reason != UpdateReason.NewTick;

        TValue result = _squeeze.Update(bar, isNew);

        if (!ShowColdValues && !_squeeze.IsHot)
        {
            return;
        }

        int offset = args.Reason == UpdateReason.HistoricalBar ? 0 : -1;

        // Set momentum histogram with color coding
        _momentumSeries.SetValue(result.Value, offset);

        // Set momentum color based on direction and sign
        Color momentumColor = _squeeze.ColorCode switch
        {
            0 => Color.Cyan,   // Rising above zero
            1 => Color.Blue,   // Falling above zero
            2 => Color.Red,    // Falling below zero
            3 => Color.Yellow, // Rising below zero
            _ => Color.Cyan
        };
        _momentumSeries.SetMarker(offset, momentumColor);

        // Set squeeze indicator - dot at zero line
        _squeezeOnSeries.SetValue(0, offset);

        // Red dot = squeeze on, Green dot = squeeze off
        Color squeezeColor = _squeeze.SqueezeOn ? Color.Red : Color.Green;
        _squeezeOnSeries.SetMarker(offset, squeezeColor);
    }
}
