using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Tr _tr = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "TR";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/tr/Tr.Quantower.cs";

    public TrIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "TR - True Range";
        Description = "True Range measures the maximum price movement including gaps from the previous close. It is the foundation for ATR (Average True Range).";

        _series = new LineSeries(name: "TR", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _tr = new Tr();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _tr.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _tr.IsHot, ShowColdValues);
    }
}