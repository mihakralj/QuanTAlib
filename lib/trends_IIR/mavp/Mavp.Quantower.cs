using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MavpIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Min Period", sortIndex: 2, 1, 200, 1, 0)]
    public int MinPeriod { get; set; } = 2;

    [InputParameter("Max Period", sortIndex: 3, 1, 200, 1, 0)]
    public int MaxPeriod { get; set; } = 30;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Mavp _mavp = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MAVP {Period}:{_sourceName}";

    public MavpIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "MAVP - Moving Average Variable Period";
        Description = "EMA with per-bar variable smoothing period";
        _series = new LineSeries(name: $"MAVP {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _mavp = new Mavp(MinPeriod, MaxPeriod);
        _mavp.Period = Period;
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _mavp.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _series.SetValue(value, _mavp.IsHot, ShowColdValues);
    }
}
