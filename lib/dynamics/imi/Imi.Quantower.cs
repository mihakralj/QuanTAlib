using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ImiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Imi _imi = null!;
    private readonly LineSeries _imiSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"IMI {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/imi/Imi.Quantower.cs";

    public ImiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Intraday Momentum Index";
        Description = "Technical indicator combining candlestick analysis with RSI-like calculation (Tushar Chande)";

        _imiSeries = new LineSeries(name: "IMI", color: Color.Yellow, width: 2, style: LineStyle.Solid);

        AddLineSeries(_imiSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _imi = new Imi(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _imi.Update(this.GetInputBar(args), args.IsNewBar());

        _imiSeries.SetValue(result.Value, _imi.IsHot, ShowColdValues);
    }
}
