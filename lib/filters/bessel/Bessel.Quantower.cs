using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class BesselIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Length", sortIndex: 1, 1, 1000, 1, 0)]
    public int Length { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Bessel _filter = null!;
    protected LineSeries Series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BESSEL {Length}:{SourceName}";

    public BesselIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "BESSEL - Bessel Filter";
        Description = "2nd-order Bessel low-pass filter with maximally flat group delay";
        Series = new LineSeries(name: $"BESSEL {Length}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _filter = new Bessel(Length);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        TValue result = _filter.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());

        Series.SetValue(result.Value, _filter.IsHot, ShowColdValues);
    }
}
