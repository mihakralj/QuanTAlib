using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for YZVAMA (Yang-Zhang Volatility Adjusted Moving Average).
/// YZVAMA requires OHLC data to compute Yang-Zhang volatility.
/// </summary>
[SkipLocalsInit]
public class YzvamaIndicator : Indicator, IWatchlistIndicator
{
    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Short YZV Period", sortIndex: 2, 1, 100, 1, 0)]
    public int ShortYzvPeriod { get; set; } = 3;

    [InputParameter("Long YZV Period", sortIndex: 3, 1, 500, 1, 0)]
    public int LongYzvPeriod { get; set; } = 50;

    [InputParameter("Percentile Lookback", sortIndex: 4, 1, 2000, 1, 0)]
    public int PercentileLookback { get; set; } = 100;

    [InputParameter("Min Length", sortIndex: 5, 1, 500, 1, 0)]
    public int MinLength { get; set; } = 5;

    [InputParameter("Max Length", sortIndex: 6, 1, 2000, 1, 0)]
    public int MaxLength { get; set; } = 100;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Yzvama ma = null!;
    protected LineSeries Series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"YZVAMA {ShortYzvPeriod},{LongYzvPeriod},{PercentileLookback}:{Source}";

    public YzvamaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "YZVAMA - Yang-Zhang Volatility Adjusted Moving Average";
        Description = "Adjusts MA length based on percentile rank of short-term Yang-Zhang volatility";
        Series = new LineSeries(name: "YZVAMA", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Yzvama(ShortYzvPeriod, LongYzvPeriod, PercentileLookback, MinLength, MaxLength);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        var bar = new TBar(
            item.TimeLeft.Ticks,
            item[PriceType.Open],
            item[PriceType.High],
            item[PriceType.Low],
            item[PriceType.Close],
            item[PriceType.Volume]);

        double source = _priceSelector(item);
        TValue result = ma.Update(bar, source, isNew: args.IsNewBar());
        Series.SetValue(result.Value, ma.IsHot, ShowColdValues);
    }
}
