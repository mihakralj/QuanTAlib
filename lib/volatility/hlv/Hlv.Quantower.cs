using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HlvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Annualize", sortIndex: 2)]
    public bool Annualize { get; set; } = true;

    [InputParameter("Annual Periods", sortIndex: 3, 1, 365, 1, 0)]
    public int AnnualPeriods { get; set; } = 252;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hlv _hlv = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HLV {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/hlv/Hlv.Quantower.cs";

    public HlvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HLV - High-Low Volatility (Parkinson)";
        Description = "High-Low Volatility is a range-based volatility estimator using only High-Low prices (Parkinson method), providing efficient estimates without requiring Open-Close data";

        _series = new LineSeries(name: "HLV", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _hlv = new Hlv(Period, Annualize, AnnualPeriods);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _hlv.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _hlv.IsHot, ShowColdValues);
    }
}