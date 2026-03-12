using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Variance Quantower indicator.
/// Measures the dispersion of data points around their mean over a rolling window.
/// </summary>
[SkipLocalsInit]
public class VarianceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Population", sortIndex: 2, variants: new object[] {
        "Sample (N-1)", false, "Population (N)", true })]
    public bool IsPopulation { get; set; } = false;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Variance _indicator = null!;
    protected LineSeries Series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VAR({Period},{(IsPopulation ? "Pop" : "Samp")})";

    public VarianceIndicator()
    {
        OnBackGround = false;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "VAR - Variance";
        Description = "Measures the dispersion of a set of data points around their mean over a rolling window.";
        Series = new LineSeries(name: "Variance", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _indicator = new Variance(Period, IsPopulation);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _indicator.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        Series.SetValue(result.Value, _indicator.IsHot, ShowColdValues);
    }
}
