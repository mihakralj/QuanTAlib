using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class DwmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dwma? ma;
    private int _warmupBarIndex = -1;
    protected LineSeries? Series;
    protected string? SourceName;

    public int MinHistoryDepths => Period * 2; // DWMA needs roughly 2x period to warm up
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DWMA {Period}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/dwma/Dwma.Quantower.cs";

    public DwmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "DWMA - Double Weighted Moving Average";
        Description = "Double Weighted Moving Average";
        Series = new(name: $"DWMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Dwma(Period);
        _warmupBarIndex = -1;
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        TValue result = ma!.Update(input, isNew);
        if (_warmupBarIndex < 0 && ma!.IsHot)
            _warmupBarIndex = Count;
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, _warmupBarIndex, showColdValues: ShowColdValues, tension: 0.2);
    }
}
