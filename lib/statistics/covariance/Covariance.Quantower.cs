using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CovarianceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    [InputParameter("Source 1", sortIndex: 3)]
    public SourceType Source1 { get; set; } = SourceType.Close;

    [InputParameter("Source 2", sortIndex: 4)]
    public SourceType Source2 { get; set; } = SourceType.Open;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Covariance? _cov;
    private readonly LineSeries? _series;
    private Func<IHistoryItem, double>? _priceSelector1;
    private Func<IHistoryItem, double>? _priceSelector2;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Cov({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/covariance/Covariance.Quantower.cs";

    public CovarianceIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Covariance";
        Description = "Measures the joint variability of two random variables.";

        _series = new(name: "Covariance", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _cov = new Covariance(Period, IsPopulation);
        _priceSelector1 = Source1.GetPriceSelector();
        _priceSelector2 = Source2.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double val1 = _priceSelector1!(item);
        double val2 = _priceSelector2!(item);
        var time = this.HistoricalData.Time();

        var input1 = new TValue(time, val1);
        var input2 = new TValue(time, val2);
        
        TValue result = _cov!.Update(input1, input2, args.IsNewBar());

        _series!.SetValue(result.Value, _cov.IsHot, ShowColdValues);
    }
}
