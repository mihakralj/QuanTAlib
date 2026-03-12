using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MinusDmIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private MinusDm _minusDm = null!;
    private readonly LineSeries _minusDmSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"-DM {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/minusdm/MinusDm.Quantower.cs";

    public MinusDmIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "-DM - Minus Directional Movement";
        Description = "Wilder-smoothed downward directional movement in price units";

        _minusDmSeries = new LineSeries(name: "-DM", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_minusDmSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _minusDm = new MinusDm(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _minusDm.Update(this.GetInputBar(args), args.IsNewBar());

        _minusDmSeries.SetValue(result.Value, _minusDm.IsHot, ShowColdValues);
    }
}
