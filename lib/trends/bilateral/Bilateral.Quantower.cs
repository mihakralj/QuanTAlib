using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class BilateralIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Sigma Spatial Ratio", sortIndex: 2, 0.1, 100, 0.1, 2)]
    public double SigmaSRatio { get; set; } = 0.5;

    [InputParameter("Sigma Range Multiplier", sortIndex: 3, 0.1, 100, 0.1, 2)]
    public double SigmaRMult { get; set; } = 1.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Bilateral? _bilateral;
    protected LineSeries? Series;
    protected string? SourceName;
    private int _warmupBarIndex = -1;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Bilateral {Period}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/bilateral/Bilateral.Quantower.cs";

    public BilateralIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "Bilateral Filter";
        Description = "Bilateral Filter";
        Series = new(name: $"Bilateral {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _bilateral = new Bilateral(Period, SigmaSRatio, SigmaRMult);
        SourceName = Source.ToString();
        _warmupBarIndex = -1;
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        TValue result = _bilateral!.Update(input, isNew);
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);

        if (_warmupBarIndex < 0 && _bilateral!.IsHot)
            _warmupBarIndex = Count;
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var savedColor = Series!.Color;
        Series.Color = Color.Transparent;
        base.OnPaintChart(args);
        Series.Color = savedColor;

        int warmupPeriod = _warmupBarIndex > 0 ? _warmupBarIndex : Count;
        this.PaintLine(args, Series!, warmupPeriod, showColdValues: ShowColdValues);
    }
}
