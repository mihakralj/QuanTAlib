using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AdrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Method", sortIndex: 2, variants: new object[] {
        "SMA", AdrMethod.Sma,
        "EMA", AdrMethod.Ema,
        "WMA", AdrMethod.Wma
    })]
    public AdrMethod Method { get; set; } = AdrMethod.Sma;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adr _adr = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ADR {Period} {Method}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/adr/Adr.Quantower.cs";

    public AdrIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADR - Average Daily Range";
        Description = "Measures the average price movement range over a specified period";

        _series = new LineSeries(name: "ADR", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _adr = new Adr(Period, Method);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _adr.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _adr.IsHot, ShowColdValues);
    }
}