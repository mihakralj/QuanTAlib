using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Long Period", sortIndex: 1, 1, 500, 1, 0)]
    public int LongPeriod { get; set; } = 25;

    [InputParameter("Short Period", sortIndex: 2, 1, 100, 1, 0)]
    public int ShortPeriod { get; set; } = 13;

    [InputParameter("Signal Period", sortIndex: 3, 1, 100, 1, 0)]
    public int SignalPeriod { get; set; } = 13;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Tsi _tsi = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _signalSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"TSI({LongPeriod},{ShortPeriod},{SignalPeriod}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/tsi/Tsi.Quantower.cs";

    public TsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "TSI - True Strength Index";
        Description = "Momentum oscillator using double-smoothed EMA of price momentum";

        _series = new LineSeries(name: "TSI", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Red, width: 1, style: LineStyle.Solid);
        AddLineSeries(_series);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _tsi = new Tsi(LongPeriod, ShortPeriod, SignalPeriod);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _tsi.Update(new TValue(this.GetInputBar(args).Time, _priceSelector(HistoricalData[Count - 1, SeekOriginHistory.Begin])), args.IsNewBar());

        _series.SetValue(result.Value, _tsi.IsHot, ShowColdValues);
        _series.SetMarker(0, Color.Transparent);

        _signalSeries.SetValue(_tsi.Signal, _tsi.IsHot, ShowColdValues);
        _signalSeries.SetMarker(0, Color.Transparent);
    }
}
