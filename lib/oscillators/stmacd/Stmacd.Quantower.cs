using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class StmacdIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 2, 500, 1, 0)]
    public int Periods { get; set; } = 45;

    [InputParameter("Fast Length", sortIndex: 2, 2, 500, 1, 0)]
    public int FastLength { get; set; } = 12;

    [InputParameter("Slow Length", sortIndex: 3, 2, 500, 1, 0)]
    public int SlowLength { get; set; } = 26;

    [InputParameter("Signal Length", sortIndex: 4, 2, 500, 1, 0)]
    public int SignalLength { get; set; } = 9;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Stmacd _stmacd = null!;
    private readonly LineSeries _stmacdSeries;
    private readonly LineSeries _signalSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"STMACD {Periods},{FastLength},{SlowLength},{SignalLength}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/stmacd/Stmacd.cs";

    public StmacdIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "STMACD";
        Description = "Stochastic MACD Oscillator (Apirine)";

        _stmacdSeries = new LineSeries(name: "STMACD", color: Color.DodgerBlue, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.OrangeRed, width: 1, style: LineStyle.Solid);

        AddLineSeries(_stmacdSeries);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _stmacd = new Stmacd(Periods, FastLength, SlowLength, SignalLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _stmacd.Update(this.GetInputBar(args), args.IsNewBar());

        _stmacdSeries.SetValue(_stmacd.StmacdValue.Value, _stmacd.IsHot, ShowColdValues);
        _signalSeries.SetValue(_stmacd.Signal.Value, _stmacd.IsHot, ShowColdValues);
    }
}
