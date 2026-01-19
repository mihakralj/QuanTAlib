using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AtrnIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Atrn _atrn = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ATRN {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/atrn/Atrn.Quantower.cs";

    public AtrnIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ATRN - Average True Range Normalized";
        Description = "Normalizes ATR to [0,1] range using min-max scaling over a lookback window";

        _series = new LineSeries(name: "ATRN", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _atrn = new Atrn(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _atrn.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _atrn.IsHot, ShowColdValues);
    }
}