using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class HoltIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Gamma", sortIndex: 2, 0.0, 1.0, 0.01, 2)]
    public double Gamma { get; set; } = 0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Holt _holt = null!;
    protected LineSeries Series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HOLT {Period}:{SourceName}";

    public HoltIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HOLT - Holt Exponential Moving Average";
        Description = "Double exponential smoothing tracking level and trend";
        Series = new LineSeries(name: $"HOLT {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _holt = new Holt(Period, Gamma);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _holt.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        Series.SetValue(result.Value, _holt.IsHot, ShowColdValues);
    }
}
