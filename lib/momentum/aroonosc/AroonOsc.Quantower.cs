using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AroonOscIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private AroonOsc? _aroonOsc;
    private readonly LineSeries? _oscSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AroonOsc {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/aroonosc/AroonOsc.Quantower.cs";

    public AroonOscIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Aroon Oscillator";
        Description = "Aroon Oscillator";

        _oscSeries = new(name: "Aroon Osc", color: Color.Blue, width: 2, style: LineStyle.Solid);

        AddLineSeries(_oscSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _aroonOsc = new AroonOsc(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _aroonOsc!.Update(this.GetInputBar(args), args.IsNewBar());

        _oscSeries!.SetValue(result.Value, _aroonOsc.IsHot, ShowColdValues);
    }
}
