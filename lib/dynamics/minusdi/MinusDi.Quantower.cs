using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MinusDiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private MinusDi _minusDi = null!;
    private readonly LineSeries _minusDiSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"-DI {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/minusdi/MinusDi.Quantower.cs";

    public MinusDiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "-DI - Minus Directional Indicator";
        Description = "Measures downward directional movement as a percentage of true range";

        _minusDiSeries = new LineSeries(name: "-DI", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_minusDiSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _minusDi = new MinusDi(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _minusDi.Update(this.GetInputBar(args), args.IsNewBar());

        _minusDiSeries.SetValue(result.Value, _minusDi.IsHot, ShowColdValues);
    }
}
