using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ReverseEmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private ReverseEma _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"REVERSEEMA {Period}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_IIR/reverseema/ReverseEma.Quantower.cs";

    public ReverseEmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "REVERSEEMA - Ehlers Reverse EMA";
        Description = "Removes EMA lag via 8-stage cascaded Z-transform inversion";
        _series = new LineSeries(name: $"REVERSEEMA {Period}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ma = new ReverseEma(Period);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        _series.SetValue(result.Value, _ma.IsHot, ShowColdValues);
    }
}
