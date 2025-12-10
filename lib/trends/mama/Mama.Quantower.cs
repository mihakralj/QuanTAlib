using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MamaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Limit", sortIndex: 1, 0.01, 0.99, 0.01, 2)]
    public double FastLimit { get; set; } = 0.5;

    [InputParameter("Slow Limit", sortIndex: 2, 0.01, 0.99, 0.01, 2)]
    public double SlowLimit { get; set; } = 0.05;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Mama? _ma;
    protected LineSeries? MamaSeries;
    protected LineSeries? FamaSeries;
    protected string? SourceName;
    private int _warmupBarIndex = -1;

    public static int MinHistoryDepths => 6;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MAMA({FastLimit:F2}, {SlowLimit:F2}):{SourceName}";

    public MamaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "MAMA - MESA Adaptive Moving Average";
        Description = "MESA Adaptive Moving Average";
        
        MamaSeries = new(name: "MAMA", color: Color.Red, width: 2, style: LineStyle.Solid);
        FamaSeries = new(name: "FAMA", color: Color.Blue, width: 2, style: LineStyle.Solid);
        
        AddLineSeries(MamaSeries);
        AddLineSeries(FamaSeries);
    }

    protected override void OnInit()
    {
        _ma = new Mama(FastLimit, SlowLimit);
        SourceName = Source.ToString();
        _warmupBarIndex = -1;
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        
        TValue result = _ma!.Update(input, isNew);
        
        MamaSeries!.SetValue(result.Value);
        FamaSeries!.SetValue(_ma.Fama.Value);
        
        MamaSeries!.SetMarker(0, Color.Transparent);
        FamaSeries!.SetMarker(0, Color.Transparent);

        if (_warmupBarIndex < 0 && _ma!.IsHot)
            _warmupBarIndex = Count;
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        int warmupPeriod = _warmupBarIndex > 0 ? _warmupBarIndex : Count;
        this.PaintSmoothCurve(args, MamaSeries!, warmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
        this.PaintSmoothCurve(args, FamaSeries!, warmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
