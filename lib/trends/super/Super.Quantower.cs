using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class SuperIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Multiplier", sortIndex: 2, 0.1, 100, 0.1, 1)]
    public double Multiplier { get; set; } = 3.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Super? _super;
    protected LineSeries? UpSeries;
    protected LineSeries? DownSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Super {Period}:{Multiplier}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/super/Super.Quantower.cs";

    public SuperIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "SuperTrend";
        Description = "Trend-following indicator using ATR";
        
        UpSeries = new(name: "SuperTrend Up", color: Color.Green, width: 2, style: LineStyle.Solid);
        DownSeries = new(name: "SuperTrend Down", color: Color.Red, width: 2, style: LineStyle.Solid);
        
        AddLineSeries(UpSeries);
        AddLineSeries(DownSeries);
    }

    protected override void OnInit()
    {
        _super = new Super(Period, Multiplier);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);

        TValue result = _super!.Update(bar, isNew);
        
        if (!_super.IsHot && !ShowColdValues)
        {
            return;
        }

        if (_super.IsBullish)
        {
            UpSeries!.SetValue(result.Value);
            DownSeries!.SetValue(double.NaN);
        }
        else
        {
            UpSeries!.SetValue(double.NaN);
            DownSeries!.SetValue(result.Value);
        }
    }
}
