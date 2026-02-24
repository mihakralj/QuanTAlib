using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class NwIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 64;

    [InputParameter("Bandwidth", sortIndex: 2, 0.1, 100.0, 0.5, 1)]
    public double Bandwidth { get; set; } = 8.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Nw _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 64;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"NW({Period},{Bandwidth:F1}):{_sourceName}";

    public NwIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "NW - Nadaraya-Watson Estimator";
        Description = "Gaussian kernel-weighted FIR filter using Nadaraya-Watson regression.";
        _series = new LineSeries(name: $"NW {Period}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ma = new Nw(Period, Bandwidth);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        var input = new TValue(item.TimeLeft.Ticks, _priceSelector(item));
        double value = _ma.Update(input, isNew).Value;
        _series.SetValue(value, _ma.IsHot, ShowColdValues);
    }
}
