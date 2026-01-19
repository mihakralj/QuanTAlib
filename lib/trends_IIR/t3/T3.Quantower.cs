using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class T3Indicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Volume Factor", sortIndex: 2, 0, 1, 0.01, 2)]
    public double VolumeFactor { get; set; } = 0.7;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private T3 _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"T3({Period}, {VolumeFactor:F2}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/t3/T3.Quantower.cs";

    public T3Indicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        _sourceName = Source.ToString();
        Name = "T3 - Tillson T3 Moving Average";
        Description = "Tillson T3 Moving Average";
        _series = new LineSeries(name: $"T3 {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ma = new T3(Period, VolumeFactor);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), args.IsNewBar());

        _series.SetValue(result.Value, _ma.IsHot, ShowColdValues);
        _series.SetMarker(0, Color.Transparent);
    }
}
