using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RsvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Annualize", sortIndex: 2)]
    public bool Annualize { get; set; } = true;

    [InputParameter("Annual Periods", sortIndex: 3, 1, 365, 1, 0)]
    public int AnnualPeriods { get; set; } = 252;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rsv _rsv = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RSV {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/rsv/Rsv.Quantower.cs";

    public RsvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "RSV - Rogers-Satchell Volatility";
        Description = "Rogers-Satchell Volatility is a drift-adjusted OHLC-based volatility estimator that uses all four price points (Open, High, Low, Close) to provide more accurate volatility estimates than range-based methods";

        _series = new LineSeries(name: "RSV", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _rsv = new Rsv(Period, Annualize, AnnualPeriods);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _rsv.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _rsv.IsHot, ShowColdValues);
    }
}