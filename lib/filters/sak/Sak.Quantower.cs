using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SakIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Filter Type", sortIndex: 0)]
    public string FilterType { get; set; } = "BP";

    [InputParameter("Period", sortIndex: 1, 3, 9999, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("N (order/length)", sortIndex: 2, 1, 9999, 1, 0)]
    public int N { get; set; } = 10;

    [InputParameter("Delta (BP/BS bandwidth)", sortIndex: 3, 0.01, 1.0, 0.01, 2)]
    public double Delta { get; set; } = 0.1;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Sak _sak = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SAK {FilterType}:{Period}:{_sourceName}";

    public SakIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "SAK - Swiss Army Knife Filter";
        Description = "Swiss Army Knife: 9-mode IIR/FIR filter (EMA, EHP, SMOOTH, GAUSS, BUTTER, 2PHP, BP, BS, SMA)";
        _series = new LineSeries(name: $"SAK {FilterType}:{Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _sak = new Sak(FilterType, Period, N, Delta);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _sak.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _series.SetValue(value, _sak.IsHot, ShowColdValues);
    }
}
