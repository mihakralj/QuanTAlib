using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for ADXVMA (ADX Variable Moving Average).
/// ADXVMA requires OHLC data for TR/DM/ADX calculation.
/// </summary>
[SkipLocalsInit]
public class AdxvmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 500, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adxvma ma = null!;
    protected LineSeries Series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ADXVMA {Period}";

    public AdxvmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "ADXVMA - ADX Variable Moving Average";
        Description = "Adaptive IIR filter that uses ADX as its smoothing constant";
        Series = new LineSeries(name: $"ADXVMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Adxvma(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        // ADXVMA uses OHLC for True Range and Directional Movement calculation
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
