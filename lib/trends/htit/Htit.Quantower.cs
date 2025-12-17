using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HtitIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 50; // Not used in calculation but kept for consistency if needed

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Htit? _htit;
    protected LineSeries? Series;
    protected string? SourceName;
    private int _warmupBarIndex = -1;

    public static int MinHistoryDepths => 50;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HTIT:{SourceName}";

    public HtitIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HTIT - Ehlers Hilbert Transform Instantaneous Trend";
        Description = "Ehlers Hilbert Transform Instantaneous Trend";
        Series = new(name: "HTIT", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _htit = new Htit();
        SourceName = Source.ToString();
        _warmupBarIndex = -1;
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TValue result = _htit!.Update(input, isNew);
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);

        if (_warmupBarIndex < 0 && _htit.IsHot)
            _warmupBarIndex = Count;
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        int warmupPeriod = _warmupBarIndex > 0 ? _warmupBarIndex : Count;
        this.PaintSmoothCurve(args, Series!, warmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
