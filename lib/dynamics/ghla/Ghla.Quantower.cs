using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class GhlaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 13;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ghla _ghla = null!;
    private readonly LineSeries _ghlaSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"GHLA {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/ghla/Ghla.Quantower.cs";

    public GhlaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false; // Overlay indicator — plots on price chart
        Name = "GHLA - Gann High-Low Activator";
        Description = "SMA(High)/SMA(Low) alternating trailing stop with hysteresis trend detection";

        _ghlaSeries = new LineSeries(name: "GHLA", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_ghlaSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ghla = new Ghla(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _ghla.Update(bar, args.IsNewBar());
        _ghlaSeries.SetValue(result.Value, _ghla.IsHot, ShowColdValues);
    }
}
