using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RviIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("StdDev Length", sortIndex: 1, 2, 100, 1, 0)]
    public int StdevLength { get; set; } = 10;

    [InputParameter("RMA Length", sortIndex: 2, 1, 100, 1, 0)]
    public int RmaLength { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rvi _rvi = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RVI({StdevLength},{RmaLength})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/rvi/Rvi.Quantower.cs";

    public RviIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "RVI - Relative Volatility Index";
        Description = "Relative Volatility Index measures the direction of volatility by comparing upward and downward price movements weighted by their standard deviations";

        _series = new LineSeries(name: "RVI", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _rvi = new Rvi(StdevLength, RmaLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _rvi.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _rvi.IsHot, ShowColdValues);
    }
}