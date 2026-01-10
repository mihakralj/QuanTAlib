using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class KalmanIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Process noise (Q)", sortIndex: 1, 0, 10, 0.001, 3)]
    public double Q { get; set; } = 0.01;

    [InputParameter("Measurement noise (R)", sortIndex: 2, 0, 10, 0.01, 2)]
    public double R { get; set; } = 0.1;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Kalman? _ma;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Kalman({Q:F3},{R:F2}):{_sourceName}";

    public KalmanIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "Kalman - Kalman Filter";
        Description = "Kalman Filter";
        _series = new LineSeries(name: "Kalman", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ma = new Kalman(Q, R);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _ma!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _ma.IsHot, ShowColdValues);
    }
}