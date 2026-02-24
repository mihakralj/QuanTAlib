using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class ModfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Beta", sortIndex: 2, 0.0, 1.0, 0.1, 1)]
    public double Beta { get; set; } = 0.8;

    [InputParameter("Feedback", sortIndex: 3)]
    public bool Feedback { get; set; } = false;

    [InputParameter("Feedback Weight", sortIndex: 4, 0.01, 1.0, 0.05, 2)]
    public double FbWeight { get; set; } = 0.5;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Modf _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 14;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MODF({Period},{Beta:F1}):{_sourceName}";

    public ModfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "MODF - Modular Filter";
        Description = "Dual-path adaptive filter with upper/lower EMA bands and state selection.";
        _series = new LineSeries(name: $"MODF {Period}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ma = new Modf(Period, Beta, Feedback, FbWeight);
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
