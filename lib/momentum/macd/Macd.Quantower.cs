using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MacdIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Signal Period", sortIndex: 3, 1, 2000, 1, 0)]
    public int SignalPeriod { get; set; } = 9;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Macd? _macd;
    private readonly LineSeries? _macdSeries;
    private readonly LineSeries? _signalSeries;
    private readonly LineSeries? _histSeries;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MACD({FastPeriod},{SlowPeriod},{SignalPeriod}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/macd/Macd.Quantower.cs";

    public MacdIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "MACD - Moving Average Convergence Divergence";
        Description = "Trend-following momentum indicator";

        _macdSeries = new LineSeries(name: "MACD", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Red, width: 2, style: LineStyle.Solid);
        _histSeries = new LineSeries(name: "Histogram", color: Color.Green, width: 2, style: LineStyle.Solid);

        AddLineSeries(_macdSeries);
        AddLineSeries(_signalSeries);
        AddLineSeries(_histSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _macd = new Macd(FastPeriod, SlowPeriod, SignalPeriod);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _macd!.Update(new TValue(this.GetInputBar(args).Time, _priceSelector!(HistoricalData[Count - 1, SeekOriginHistory.Begin])), args.IsNewBar());

        _macdSeries!.SetValue(result.Value, _macd.IsHot, ShowColdValues);
        _signalSeries!.SetValue(_macd.Signal.Value, _macd.IsHot, ShowColdValues);
        _histSeries!.SetValue(_macd.Histogram.Value, _macd.IsHot, ShowColdValues);
    }
}
