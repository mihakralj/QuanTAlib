using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HhllsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 500, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hhlls _hhlls = null!;
    private readonly LineSeries _hhsSeries;
    private readonly LineSeries _llsSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HHLLS {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/hhlls/Hhlls.cs";

    public HhllsIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HHLLS";
        Description = "Higher Highs & Lower Lows Stochastics (Apirine)";

        _hhsSeries = new LineSeries(name: "HHS", color: Color.Green, width: 2, style: LineStyle.Solid);
        _llsSeries = new LineSeries(name: "LLS", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_hhsSeries);
        AddLineSeries(_llsSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _hhlls = new Hhlls(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _hhlls.Update(this.GetInputBar(args), args.IsNewBar());

        _hhsSeries.SetValue(_hhlls.Hhs.Value, _hhlls.IsHot, ShowColdValues);
        _llsSeries.SetValue(_hhlls.Lls.Value, _hhlls.IsHot, ShowColdValues);
    }
}
