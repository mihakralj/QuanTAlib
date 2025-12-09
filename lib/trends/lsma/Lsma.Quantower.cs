using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class LsmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Offset", sortIndex: 2, -1000, 1000, 1, 0)]
    public int Offset { get; set; } = 0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Lsma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    private int _warmupBarIndex = -1;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LSMA {Period}:{Offset}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/lsma/Lsma.Quantower.cs";

    public LsmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "LSMA - Least Squares Moving Average";
        Description = "Least Squares Moving Average";
        Series = new(name: $"LSMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Lsma(Period, Offset);
        SourceName = Source.ToString();
        _warmupBarIndex = -1;  // Reset warmup tracking when period changes
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        TValue result = ma!.Update(input, isNew);
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here

        // Track when IsHot becomes true for the first time
        if (_warmupBarIndex < 0 && ma!.IsHot)
            _warmupBarIndex = Count;
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        int warmupPeriod = _warmupBarIndex > 0 ? _warmupBarIndex : Count;
        this.PaintSmoothCurve(args, Series!, warmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
