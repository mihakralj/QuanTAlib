using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for VAMA (Volatility Adjusted Moving Average).
/// VAMA requires OHLC data for True Range calculation to measure volatility.
/// </summary>
[SkipLocalsInit]
public class VamaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Base Length", sortIndex: 1, 1, 500, 1, 0)]
    public int BaseLength { get; set; } = 20;

    [InputParameter("Short ATR Period", sortIndex: 2, 1, 100, 1, 0)]
    public int ShortAtrPeriod { get; set; } = 10;

    [InputParameter("Long ATR Period", sortIndex: 3, 1, 500, 1, 0)]
    public int LongAtrPeriod { get; set; } = 50;

    [InputParameter("Min Length", sortIndex: 4, 1, 100, 1, 0)]
    public int MinLength { get; set; } = 5;

    [InputParameter("Max Length", sortIndex: 5, 1, 500, 1, 0)]
    public int MaxLength { get; set; } = 100;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vama ma = null!;
    protected LineSeries Series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VAMA {BaseLength},{ShortAtrPeriod},{LongAtrPeriod}";

    public VamaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "VAMA - Volatility Adjusted Moving Average";
        Description = "Dynamically adjusts MA length based on ATR volatility ratio";
        Series = new LineSeries(name: $"VAMA {BaseLength}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Vama(BaseLength, ShortAtrPeriod, LongAtrPeriod, MinLength, MaxLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        // VAMA uses OHLC for True Range calculation
        var bar = new TBar(
            item.TimeLeft.Ticks,
            item[PriceType.Open],
            item[PriceType.High],
            item[PriceType.Low],
            item[PriceType.Close],
            item[PriceType.Volume]);

        TValue result = ma.Update(bar, isNew: args.IsNewBar());
        Series.SetValue(result.Value, ma.IsHot, ShowColdValues);
    }
}
