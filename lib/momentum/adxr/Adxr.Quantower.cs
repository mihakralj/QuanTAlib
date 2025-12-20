using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AdxrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adxr? _adxr;
    protected LineSeries? AdxrSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ADXR {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/adxr/Adxr.Quantower.cs";

    public AdxrIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADXR - Average Directional Movement Rating";
        Description = "Quantifies the change in momentum of the ADX";

        AdxrSeries = new(name: "ADXR", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(AdxrSeries);
    }

    protected override void OnInit()
    {
        _adxr = new Adxr(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);

        TValue result = _adxr!.Update(bar, isNew);

        if (!_adxr.IsHot && !ShowColdValues)
        {
            return;
        }

        AdxrSeries!.SetValue(result.Value);
    }
}
