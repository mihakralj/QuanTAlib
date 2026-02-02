using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Annualize", sortIndex: 2)]
    public bool Annualize { get; set; } = true;

    [InputParameter("Annual Periods", sortIndex: 3, 1, 365, 1, 0)]
    public int AnnualPeriods { get; set; } = 252;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hv _hv = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HV {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/hv/Hv.Quantower.cs";

    public HvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HV - Historical Volatility (Close-to-Close)";
        Description = "Historical Volatility measures price volatility using standard deviation of logarithmic returns, the classical close-to-close volatility estimator";

        _series = new LineSeries(name: "HV", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _hv = new Hv(Period, Annualize, AnnualPeriods);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _hv.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _hv.IsHot, ShowColdValues);
    }
}