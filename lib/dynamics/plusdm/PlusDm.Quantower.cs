using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PlusDmIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private PlusDm _plusDm = null!;
    private readonly LineSeries _plusDmSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"+DM {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/plusdm/PlusDm.Quantower.cs";

    public PlusDmIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "+DM - Plus Directional Movement";
        Description = "Wilder-smoothed upward directional movement in price units";

        _plusDmSeries = new LineSeries(name: "+DM", color: Color.Green, width: 2, style: LineStyle.Solid);

        AddLineSeries(_plusDmSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _plusDm = new PlusDm(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _plusDm.Update(this.GetInputBar(args), args.IsNewBar());

        _plusDmSeries.SetValue(result.Value, _plusDm.IsHot, ShowColdValues);
    }
}
