using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HtTrendmodeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Data source", 10)]
    public SourceType SourceInput { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private HtTrendmode _indicator = null!;
    private readonly LineSeries _trendModeSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "HT_TRENDMODE";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/ht_trendmode/HtTrendmode.Quantower.cs";

    public HtTrendmodeIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HT_TRENDMODE - Hilbert Transform Trend Mode";
        Description = "Determines if market is trending (1) or cycling (0)";

        _trendModeSeries = new LineSeries(name: "TrendMode", color: Color.Blue, width: 3, style: LineStyle.Solid);
        AddLineSeries(_trendModeSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new HtTrendmode();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        double value = SourceInput switch
        {
            SourceType.Open => GetPrice(PriceType.Open),
            SourceType.High => GetPrice(PriceType.High),
            SourceType.Low => GetPrice(PriceType.Low),
            SourceType.Close => GetPrice(PriceType.Close),
            SourceType.HL2 => (GetPrice(PriceType.High) + GetPrice(PriceType.Low)) / 2,
            SourceType.HLC3 => (GetPrice(PriceType.High) + GetPrice(PriceType.Low) + GetPrice(PriceType.Close)) / 3,
            SourceType.OHLC4 => (GetPrice(PriceType.Open) + GetPrice(PriceType.High) + GetPrice(PriceType.Low) + GetPrice(PriceType.Close)) / 4,
            SourceType.HLCC4 => (GetPrice(PriceType.High) + GetPrice(PriceType.Low) + 2 * GetPrice(PriceType.Close)) / 4,
            _ => GetPrice(PriceType.Close)
        };

        bool isNew = args.IsNewBar();
        var result = _indicator.Update(new TValue(Time(), value), isNew);
        _trendModeSeries.SetValue(result.Value, _indicator.IsHot, ShowColdValues);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetPrice(PriceType priceType)
    {
        return HistoricalData[0, SeekOriginHistory.End][priceType];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DateTime Time()
    {
        return HistoricalData[0, SeekOriginHistory.End].TimeLeft;
    }
}
