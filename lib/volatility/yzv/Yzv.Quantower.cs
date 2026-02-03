using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class YzvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 200, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Yzv _yzv = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"YZV({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/yzv/Yzv.Quantower.cs";

    public YzvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "YZV - Yang-Zhang Volatility";
        Description = "Yang-Zhang Volatility combines overnight (close-to-open) and intraday (Rogers-Satchell) volatility components for more accurate volatility estimation";

        _series = new LineSeries(name: "YZV", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _yzv = new Yzv(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _yzv.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _yzv.IsHot, ShowColdValues);
    }
}