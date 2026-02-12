using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class StochIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("K Length", sortIndex: 1, 1, 500, 1, 0)]
    public int KLength { get; set; } = 14;

    [InputParameter("D Period", sortIndex: 2, 1, 50, 1, 0)]
    public int DPeriod { get; set; } = 3;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Stoch _stoch = null!;
    private readonly LineSeries _kSeries;
    private readonly LineSeries _dSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"STOCH {KLength},{DPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/stoch/Stoch.cs";

    public StochIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "STOCH";
        Description = "Stochastic Oscillator with %K and %D lines";

        _kSeries = new LineSeries(name: "K", color: Color.Green, width: 2, style: LineStyle.Solid);
        _dSeries = new LineSeries(name: "D", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_kSeries);
        AddLineSeries(_dSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _stoch = new Stoch(KLength, DPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _stoch.Update(this.GetInputBar(args), args.IsNewBar());

        _kSeries.SetValue(_stoch.K.Value, _stoch.IsHot, ShowColdValues);
        _dSeries.SetValue(_stoch.D.Value, _stoch.IsHot, ShowColdValues);
    }
}
