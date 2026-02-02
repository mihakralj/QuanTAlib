using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EwmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Annualize", sortIndex: 2)]
    public bool AnnualizeVol { get; set; } = true;

    [InputParameter("Annual Periods", sortIndex: 3, 1, 365, 1, 0)]
    public int AnnualPeriods { get; set; } = 252;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ewma _ewma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => AnnualizeVol
        ? $"EWMA {Period},{AnnualPeriods}:{_sourceName}"
        : $"EWMA {Period}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/ewma/Ewma.Quantower.cs";

    public EwmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "EWMA - Exponentially Weighted Moving Average Volatility";
        Description = "EWMA Volatility calculates volatility using an exponentially weighted moving average of squared log returns with bias correction";

        _series = new LineSeries(name: "EWMA", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ewma = new Ewma(Period, AnnualizeVol, AnnualPeriods);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ewma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        _series.SetValue(result.Value, _ewma.IsHot, ShowColdValues);
    }
}