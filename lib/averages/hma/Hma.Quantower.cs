using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hma? ma;
    private int _warmupBarIndex = -1;
    protected LineSeries? Series;
    protected string? SourceName;

    public int MinHistoryDepths => Period + (int)Math.Sqrt(Period); // Approximate warmup
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HMA {Period}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/averages/hma/Hma.cs";

    public HmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HMA - Hull Moving Average";
        Description = "Hull Moving Average for reduced lag";
        Series = new(name: $"HMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Hma(Period);
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
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, _warmupBarIndex, showColdValues: ShowColdValues, tension: 0.2);
    }
}
