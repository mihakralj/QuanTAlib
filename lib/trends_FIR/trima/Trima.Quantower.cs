using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TrimaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Trima _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"TRIMA {Period}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/trima/Trima.Quantower.cs";

    public TrimaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        _sourceName = Source.ToString();
        Name = "TRIMA - Triangular Moving Average";
        Description = "Triangular Moving Average";
        _series = new LineSeries(name: $"TRIMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ma = new Trima(Period);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar && args.Reason != UpdateReason.NewTick)
            return;

        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), args.IsNewBar());

        _series.SetValue(result.Value, _ma.IsHot, ShowColdValues);
        _series.SetMarker(0, Color.Transparent);
    }
}
