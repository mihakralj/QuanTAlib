using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ButterIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Butter? _ma;
    protected LineSeries? _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BUTTER {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/butter/Butter.Quantower.cs";

    public ButterIndicator()
    {
        Name = "BUTTER - Butterworth Filter";
        Description = "A 2nd-order low-pass filter with maximally flat frequency response in the passband.";
        SeparateWindow = false;

        _series = new(name: "BUTTER", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ma = new Butter(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        TValue result = _ma!.Update(input, isNew);

        if (!_ma.IsHot && !ShowColdValues)
        {
            return;
        }

        _series!.SetValue(result.Value);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, _series!, _ma!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
