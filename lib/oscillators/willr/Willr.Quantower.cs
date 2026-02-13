using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class WillrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, 1, 500, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Willr _indicator = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _overbought;
    private readonly LineSeries _oversold;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"WILLR({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/willr/Willr.cs";

    public WillrIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "WILLR - Williams %R";
        Description = "Williams %R oscillator. Measures close position relative to highest high over lookback period. Range: -100 to 0.";

        _series = new LineSeries(name: "Williams %R", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _overbought = new LineSeries(name: "Overbought", color: Color.Gray, width: 1, style: LineStyle.Dash);
        _oversold = new LineSeries(name: "Oversold", color: Color.Gray, width: 1, style: LineStyle.Dash);

        AddLineSeries(_series);
        AddLineSeries(_overbought);
        AddLineSeries(_oversold);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Willr(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _series.SetValue(_indicator.Last.Value, _indicator.IsHot, ShowColdValues);
        _overbought.SetValue(-20.0, _indicator.IsHot, ShowColdValues);
        _oversold.SetValue(-80.0, _indicator.IsHot, ShowColdValues);
    }
}
