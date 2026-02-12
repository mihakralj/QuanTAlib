using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class KdjIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Length", sortIndex: 1, 1, 500, 1, 0)]
    public int Length { get; set; } = 9;

    [InputParameter("Signal", sortIndex: 2, 1, 50, 1, 0)]
    public int Signal { get; set; } = 3;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Kdj _kdj = null!;
    private readonly LineSeries _kSeries;
    private readonly LineSeries _dSeries;
    private readonly LineSeries _jSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"KDJ {Length},{Signal}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/kdj/Kdj.Quantower.cs";

    public KdjIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "KDJ";
        Description = "Enhanced Stochastic Oscillator with K, D, J lines";

        _kSeries = new LineSeries(name: "K", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _dSeries = new LineSeries(name: "D", color: Color.Red, width: 2, style: LineStyle.Solid);
        _jSeries = new LineSeries(name: "J", color: Color.Yellow, width: 2, style: LineStyle.Solid);

        AddLineSeries(_kSeries);
        AddLineSeries(_dSeries);
        AddLineSeries(_jSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _kdj = new Kdj(Length, Signal);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _kdj.Update(this.GetInputBar(args), args.IsNewBar());

        _kSeries.SetValue(_kdj.K.Value, _kdj.IsHot, ShowColdValues);
        _dSeries.SetValue(_kdj.D.Value, _kdj.IsHot, ShowColdValues);
        _jSeries.SetValue(result.Value, _kdj.IsHot, ShowColdValues);
    }
}
