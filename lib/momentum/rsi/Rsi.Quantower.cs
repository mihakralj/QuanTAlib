using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rsi _rsi = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RSI({Period}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/rsi/Rsi.Quantower.cs";

    public RsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "RSI - Relative Strength Index";
        Description = "Measures the speed and change of price movements";

        _series = new LineSeries(name: "RSI", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _rsi = new Rsi(Period);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _rsi.Update(new TValue(this.GetInputBar(args).Time, _priceSelector(HistoricalData[Count - 1, SeekOriginHistory.Begin])), args.IsNewBar());

        _series.SetValue(result.Value, _rsi.IsHot, ShowColdValues);
        _series.SetMarker(0, Color.Transparent);
    }
}
