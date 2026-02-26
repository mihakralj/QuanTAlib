using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RaviIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Short Period", sortIndex: 1, 1, 100, 1, 0)]
    public int ShortPeriod { get; set; } = 7;

    [InputParameter("Long Period", sortIndex: 2, 2, 500, 1, 0)]
    public int LongPeriod { get; set; } = 65;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ravi _ravi = null!;
    private readonly LineSeries _raviSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RAVI {ShortPeriod},{LongPeriod}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/ravi/Ravi.Quantower.cs";

    public RaviIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "RAVI - Chande Range Action Verification Index";
        Description = "Measures trend strength via |SMA(short) - SMA(long)| / SMA(long) × 100";

        _raviSeries = new LineSeries(name: "RAVI", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_raviSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ravi = new Ravi(ShortPeriod, LongPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _ravi.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _raviSeries.SetValue(value, _ravi.IsHot, ShowColdValues);
    }
}
