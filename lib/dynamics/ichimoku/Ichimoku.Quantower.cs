using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Ichimoku Kinko Hyo (One Glance Equilibrium Chart) for Quantower.
/// Displays all five Ichimoku components: Tenkan-sen, Kijun-sen, Senkou Span A/B, and Chikou Span.
/// The cloud (Kumo) is formed between Senkou Span A and B.
/// </summary>
[SkipLocalsInit]
public sealed class IchimokuIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Tenkan Period", sortIndex: 1, 1, 500, 1, 0)]
    public int TenkanPeriod { get; set; } = 9;

    [InputParameter("Kijun Period", sortIndex: 2, 1, 500, 1, 0)]
    public int KijunPeriod { get; set; } = 26;

    [InputParameter("Senkou B Period", sortIndex: 3, 1, 500, 1, 0)]
    public int SenkouBPeriod { get; set; } = 52;

    [InputParameter("Displacement", sortIndex: 4, 1, 500, 1, 0)]
    public int Displacement { get; set; } = 26;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ichimoku _ichimoku = null!;
    private readonly LineSeries _tenkanSeries;
    private readonly LineSeries _kijunSeries;
    private readonly LineSeries _senkouASeries;
    private readonly LineSeries _senkouBSeries;
    private readonly LineSeries _chikouSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ICHIMOKU({TenkanPeriod},{KijunPeriod},{SenkouBPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/ichimoku/Ichimoku.Quantower.cs";

    public IchimokuIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false; // Overlay on price chart
        Name = "Ichimoku Kinko Hyo";
        Description = "Japanese equilibrium chart with Tenkan-sen, Kijun-sen, Senkou Spans, and Chikou Span";

        // Standard Ichimoku colors following traditional conventions
        _tenkanSeries = new LineSeries(name: "Tenkan-sen", color: Color.Blue, width: 1, style: LineStyle.Solid);
        _kijunSeries = new LineSeries(name: "Kijun-sen", color: Color.Red, width: 2, style: LineStyle.Solid);
        _senkouASeries = new LineSeries(name: "Senkou A", color: Color.Green, width: 1, style: LineStyle.Solid);
        _senkouBSeries = new LineSeries(name: "Senkou B", color: Color.Salmon, width: 1, style: LineStyle.Solid);
        _chikouSeries = new LineSeries(name: "Chikou", color: Color.Purple, width: 1, style: LineStyle.Solid);

        AddLineSeries(_tenkanSeries);
        AddLineSeries(_kijunSeries);
        AddLineSeries(_senkouASeries);
        AddLineSeries(_senkouBSeries);
        AddLineSeries(_chikouSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ichimoku = new Ichimoku(TenkanPeriod, KijunPeriod, SenkouBPeriod, Displacement);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ichimoku.Update(this.GetInputBar(args), args.IsNewBar());

        // Tenkan-sen and Kijun-sen are plotted at current bar (no offset)
        _tenkanSeries.SetValue(_ichimoku.Tenkan.Value, _ichimoku.IsHot, ShowColdValues);
        _kijunSeries.SetValue(_ichimoku.Kijun.Value, _ichimoku.IsHot, ShowColdValues);

        // Senkou Spans are plotted Displacement bars forward
        // Note: In Quantower, LineSeries offset handling may need platform-specific implementation
        // The values here represent current calculations; charting offset is handled by platform
        _senkouASeries.SetValue(_ichimoku.SenkouA.Value, _ichimoku.IsHot, ShowColdValues);
        _senkouBSeries.SetValue(_ichimoku.SenkouB.Value, _ichimoku.IsHot, ShowColdValues);

        // Chikou Span is plotted Displacement bars backward
        // Note: Similar to above, the offset is a display concern
        _chikouSeries.SetValue(_ichimoku.Chikou.Value, _ichimoku.IsHot, ShowColdValues);
    }
}
