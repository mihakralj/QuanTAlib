using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VovIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Volatility Period", sortIndex: 1, 1, 200, 1, 0)]
    public int VolatilityPeriod { get; set; } = 20;

    [InputParameter("VOV Period", sortIndex: 2, 1, 200, 1, 0)]
    public int VovPeriod { get; set; } = 10;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vov _vov = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VOV({VolatilityPeriod},{VovPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/vov/Vov.Quantower.cs";

    public VovIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VOV - Volatility of Volatility";
        Description = "Volatility of Volatility measures the standard deviation of volatility itself, quantifying how much volatility fluctuates over time";

        _series = new LineSeries(name: "VOV", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _vov = new Vov(VolatilityPeriod, VovPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vov.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _vov.IsHot, ShowColdValues);
    }
}