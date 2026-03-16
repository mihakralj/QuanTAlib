using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// ADF Quantower indicator.
/// Augmented Dickey-Fuller unit root test — outputs p-value for stationarity detection.
/// </summary>
[SkipLocalsInit]
public sealed class AdfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 20, 500, 1, 0)]
    public int Period { get; set; } = 50;

    [InputParameter("Max Lag (0=auto)", sortIndex: 2, 0, 10, 1, 0)]
    public int MaxLag { get; set; } = 0;

    [InputParameter("Regression Model", sortIndex: 3, variants: new object[] {
        "No Constant", 0, "Constant", 1, "Constant + Trend", 2 })]
    public int RegressionModel { get; set; } = 1;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adf _indicator = null!;
    private readonly LineSeries _pValueSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName
    {
        get
        {
            string regStr = RegressionModel switch { 0 => "nc", 1 => "c", 2 => "ct", _ => "c" };
            return $"ADF({Period},{MaxLag},{regStr}):{_sourceName}";
        }
    }

    public override string SourceCodeLink =>
        "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/adf/Adf.Quantower.cs";

    public AdfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "ADF - Augmented Dickey-Fuller Test";
        Description = "Tests for unit root (non-stationarity). P-value near 0 indicates stationarity.";

        _pValueSeries = new LineSeries(name: "P-Value", color: IndicatorExtensions.Statistics,
            width: 2, style: LineStyle.Solid);
        AddLineSeries(_pValueSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Adf(Period, MaxLag, (Adf.AdfRegression)RegressionModel);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _indicator.Update(
            new TValue(item.TimeLeft.Ticks, _priceSelector(item)),
            isNew: args.IsNewBar());

        _pValueSeries.SetValue(result.Value, _indicator.IsHot, ShowColdValues);
    }
}
