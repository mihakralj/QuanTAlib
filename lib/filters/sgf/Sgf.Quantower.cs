using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SgfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 5;

    [InputParameter("Polynomial Order", sortIndex: 2, 1, 100, 1, 0)]
    public int PolyOrder { get; set; } = 2;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Sgf? _sgf;
    private readonly LineSeries? _series;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SGF({Period},{PolyOrder}):{Source}";

    public SgfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "SGF - Savitzky-Golay Filter";
        Description = "Savitzky-Golay Filter (FIR)";
        _series = new LineSeries(name: $"SGF {Period}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();

        // Validation handled by SGF constructor, but Quantower might let users set invalid values
        // We ensure Period > PolyOrder via Min/Max constraints in attributes if possible,
        // but robustly we should handle it.
        // If PolyOrder >= Period, SGF constructor throws.
        // Let's ensure reasonable defaults just in case via property normalization or rely on exception.

        _sgf = new Sgf(Period, PolyOrder);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _sgf!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _sgf.IsHot, ShowColdValues);
    }
}
