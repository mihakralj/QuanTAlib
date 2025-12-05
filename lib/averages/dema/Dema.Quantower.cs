using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class DemaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dema? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    private int _warmupBarIndex = -1;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DEMA {Period}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/averages/dema/Dema.Quantower.cs";

    public DemaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "DEMA - Double Exponential Moving Average";
        Description = "Double Exponential Moving Average";
        Series = new(name: $"DEMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Dema(Period);
        SourceName = Source.ToString();
        _warmupBarIndex = -1;
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        TValue result = ma!.Update(input, isNew);
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);

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
