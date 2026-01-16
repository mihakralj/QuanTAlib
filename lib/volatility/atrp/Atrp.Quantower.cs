using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AtrpIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Atrp _atrp = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ATRP {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/atrp/Atrp.Quantower.cs";

    public AtrpIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ATRP - Average True Range Percent";
        Description = "Measures volatility as a percentage of the closing price";

        _series = new LineSeries(name: "ATRP", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _atrp = new Atrp(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _atrp.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _atrp.IsHot, ShowColdValues);
    }
}