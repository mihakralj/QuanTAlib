using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AroonOscIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private AroonOsc? _aroonOsc;
    protected LineSeries? OscSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AroonOsc {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/aroonosc/AroonOsc.Quantower.cs";

    public AroonOscIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Aroon Oscillator";
        Description = "Aroon Oscillator";

        OscSeries = new(name: "Aroon Osc", color: Color.Blue, width: 2, style: LineStyle.Solid);

        AddLineSeries(OscSeries);
    }

    protected override void OnInit()
    {
        _aroonOsc = new AroonOsc(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);

        TValue result = _aroonOsc!.Update(bar, isNew);

        if (!_aroonOsc.IsHot && !ShowColdValues)
        {
            return;
        }

        OscSeries!.SetValue(result.Value);
    }
}
