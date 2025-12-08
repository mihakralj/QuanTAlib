using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class T3Indicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Volume Factor", sortIndex: 2, 0, 1, 0.01, 2)]
    public double VolumeFactor { get; set; } = 0.7;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private T3? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    private int _warmupBarIndex = -1;

    public int MinHistoryDepths => Period * 6; // Approx warmup for 6 stages
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"T3({Period}, {VolumeFactor:F2}):{SourceName}";

    public T3Indicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "T3 - Tillson T3 Moving Average";
        Description = "Tillson T3 Moving Average";
        Series = new(name: $"T3 {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new T3(Period, VolumeFactor);
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
