using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AmatIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 500, 1, 0)]
    public int FastPeriod { get; set; } = 10;

    [InputParameter("Slow Period", sortIndex: 2, 1, 500, 1, 0)]
    public int SlowPeriod { get; set; } = 50;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Amat _amat = null!;
    private readonly LineSeries _trendSeries;
    private readonly LineSeries _strengthSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AMAT {FastPeriod},{SlowPeriod}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/amat/Amat.cs";

    public AmatIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "AMAT - Archer Moving Averages Trends";
        Description = "Trend system using fast/slow EMA alignment for directional signals";

        _trendSeries = new LineSeries(name: "Trend", color: Color.Green, width: 2, style: LineStyle.Solid);
        _strengthSeries = new LineSeries(name: "Strength", color: Color.Orange, width: 1, style: LineStyle.Solid);

        AddLineSeries(_trendSeries);
        AddLineSeries(_strengthSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _amat = new Amat(FastPeriod, SlowPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        _ = _amat.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew);

        _trendSeries.SetValue(_amat.Last.Value, _amat.IsHot, ShowColdValues);
        _strengthSeries.SetValue(_amat.Strength.Value, _amat.IsHot, ShowColdValues);
    }
}
