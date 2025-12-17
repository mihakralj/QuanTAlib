using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AroonIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Aroon? _aroon;
    protected LineSeries? UpSeries;
    protected LineSeries? DownSeries;
    protected LineSeries? OscSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Aroon {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/aroon/Aroon.Quantower.cs";

    public AroonIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Aroon";
        Description = "Identifies trend changes and strength";

        UpSeries = new(name: "Aroon Up", color: Color.Green, width: 1, style: LineStyle.Solid);
        DownSeries = new(name: "Aroon Down", color: Color.Red, width: 1, style: LineStyle.Solid);
        OscSeries = new(name: "Aroon Osc", color: Color.Blue, width: 2, style: LineStyle.Solid);

        AddLineSeries(UpSeries);
        AddLineSeries(DownSeries);
        AddLineSeries(OscSeries);
    }

    protected override void OnInit()
    {
        _aroon = new Aroon(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);

        TValue result = _aroon!.Update(bar, isNew);

        if (!_aroon.IsHot && !ShowColdValues)
        {
            return;
        }

        UpSeries!.SetValue(_aroon.Up.Value);
        DownSeries!.SetValue(_aroon.Down.Value);
        OscSeries!.SetValue(result.Value);
    }
}
