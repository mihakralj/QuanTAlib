using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class BlmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Blma? _ma;
    protected LineSeries? _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BLMA {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/blma/Blma.Quantower.cs";

    public BlmaIndicator()
    {
        Name = "BLMA - Blackman Window Moving Average";
        Description = "A moving average using the Blackman window function for superior noise suppression.";
        SeparateWindow = false;

        _series = new(name: "BLMA", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ma = new Blma(Period);
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
