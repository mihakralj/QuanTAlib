using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 5;

    [InputParameter("Smoothing Period", sortIndex: 2, 1, 1000, 1, 0)]
    public int SmoothingPeriod { get; set; } = 20;

    [InputParameter("Annualize", sortIndex: 3)]
    public bool Annualize { get; set; } = true;

    [InputParameter("Annual Periods", sortIndex: 4, 1, 365, 1, 0)]
    public int AnnualPeriods { get; set; } = 252;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rv _rv = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RV {Period},{SmoothingPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/rv/Rv.Quantower.cs";

    public RvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "RV - Realized Volatility";
        Description = "Realized Volatility measures price volatility using the sum of squared logarithmic returns, smoothed with SMA";

        _series = new LineSeries(name: "RV", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _rv = new Rv(Period, SmoothingPeriod, Annualize, AnnualPeriods);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _rv.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _rv.IsHot, ShowColdValues);
    }
}