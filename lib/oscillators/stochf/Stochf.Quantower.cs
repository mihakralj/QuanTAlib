using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class StochfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("K Length", sortIndex: 1, 1, 500, 1, 0)]
    public int KLength { get; set; } = 5;

    [InputParameter("D Period", sortIndex: 2, 1, 50, 1, 0)]
    public int DPeriod { get; set; } = 3;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Stochf _stochf = null!;
    private readonly LineSeries _kSeries;
    private readonly LineSeries _dSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"STOCHF {KLength},{DPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/stochf/Stochf.cs";

    public StochfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "STOCHF";
        Description = "Stochastic Fast Oscillator with raw %K and SMA %D lines";

        _kSeries = new LineSeries(name: "K", color: Color.Green, width: 2, style: LineStyle.Solid);
        _dSeries = new LineSeries(name: "D", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_kSeries);
        AddLineSeries(_dSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _stochf = new Stochf(KLength, DPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _stochf.Update(this.GetInputBar(args), args.IsNewBar());

        _kSeries.SetValue(_stochf.K.Value, _stochf.IsHot, ShowColdValues);
        _dSeries.SetValue(_stochf.D.Value, _stochf.IsHot, ShowColdValues);
    }
}
