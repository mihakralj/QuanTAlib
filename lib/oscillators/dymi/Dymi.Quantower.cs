using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DymiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Base RSI Period", sortIndex: 1, 2, 500, 1, 0)]
    public int BasePeriod { get; set; } = 14;

    [InputParameter("Short StdDev Period", sortIndex: 2, 2, 500, 1, 0)]
    public int ShortPeriod { get; set; } = 5;

    [InputParameter("Long StdDev Period", sortIndex: 3, 2, 500, 1, 0)]
    public int LongPeriod { get; set; } = 10;

    [InputParameter("Min Period", sortIndex: 4, 2, 500, 1, 0)]
    public int MinPeriod { get; set; } = 3;

    [InputParameter("Max Period", sortIndex: 5, 2, 500, 1, 0)]
    public int MaxPeriod { get; set; } = 30;

    [IndicatorExtensions.DataSourceInput(sortIndex: 6)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dymi _dymi = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName =>
        $"DYMI ({BasePeriod},{ShortPeriod},{LongPeriod},{MinPeriod},{MaxPeriod})";

    public override string SourceCodeLink =>
        "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/dymi/Dymi.Quantower.cs";

    public DymiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DYMI - Dynamic Momentum Index";
        Description = "Volatility-adaptive RSI by Chande & Kroll: period shortens in volatile markets, lengthens in quiet ones.";

        _series = new LineSeries("DYMI", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dymi = new Dymi(BasePeriod, ShortPeriod, LongPeriod, MinPeriod, MaxPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _dymi.Update(input, args.IsNewBar());

        if (!_dymi.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
