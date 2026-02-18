using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class ALaguerreIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Length", sortIndex: 1, 1, 200, 1, 0)]
    public int Length { get; set; } = 20;

    [InputParameter("Median Length", sortIndex: 2, 1, 50, 1, 0)]
    public int MedianLength { get; set; } = 5;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private ALaguerre ma = null!;
    protected LineSeries Series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ALAGUERRE {Length},{MedianLength}:{SourceName}";

    public ALaguerreIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "ALAGUERRE - Adaptive Laguerre Filter (Ehlers)";
        Description = "Adaptive variant of Laguerre Filter with variable alpha from tracking-error normalization and median smoothing";
        Series = new LineSeries(name: $"ALaguerre {Length},{MedianLength}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new ALaguerre(Length, MedianLength);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        Series.SetValue(result.Value, ma.IsHot, ShowColdValues);
    }
}
