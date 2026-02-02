using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Alpha", sortIndex: 2, 0.01, 0.99, 0.01, 2)]
    public double Alpha { get; set; } = 0.2;

    [InputParameter("Beta", sortIndex: 3, 0.01, 0.99, 0.01, 2)]
    public double Beta { get; set; } = 0.7;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cv _cv = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CV {Period},{Alpha:F2},{Beta:F2}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/cv/Cv.Quantower.cs";

    public CvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "CV - Conditional Volatility (GARCH(1,1))";
        Description = "Conditional Volatility calculates GARCH(1,1) volatility, modeling time-varying volatility as a function of past squared returns and past variance";

        _series = new LineSeries(name: "CV", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        // Validate GARCH stationarity constraint
        if (Alpha + Beta >= 1.0)
        {
            Beta = 0.99 - Alpha; // Adjust beta to maintain stationarity
        }

        _cv = new Cv(Period, Alpha, Beta);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _cv.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        _series.SetValue(result.Value, _cv.IsHot, ShowColdValues);
    }
}