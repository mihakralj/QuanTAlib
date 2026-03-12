using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PlusDiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private PlusDi _plusDi = null!;
    private readonly LineSeries _plusDiSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"+DI {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/plusdi/PlusDi.Quantower.cs";

    public PlusDiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "+DI - Plus Directional Indicator";
        Description = "Measures upward directional movement as a percentage of true range";

        _plusDiSeries = new LineSeries(name: "+DI", color: Color.Green, width: 2, style: LineStyle.Solid);

        AddLineSeries(_plusDiSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _plusDi = new PlusDi(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _plusDi.Update(this.GetInputBar(args), args.IsNewBar());

        _plusDiSeries.SetValue(result.Value, _plusDi.IsHot, ShowColdValues);
    }
}
