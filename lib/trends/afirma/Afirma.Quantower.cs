using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AfirmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Taps", sortIndex: 2, 1, 100, 1, 0)]
    public int Taps { get; set; } = 6;

    [InputParameter("Window", sortIndex: 3)]
    public Afirma.WindowType Window { get; set; } = Afirma.WindowType.BlackmanHarris;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Afirma? _afirma;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AFIRMA {Period},{Taps}:{_sourceName}";

    public AfirmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "AFIRMA - Autoregressive FIR Moving Average";
        Description = "Hybrid filter combining ARMA modeling, FIR filtering, and cubic spline fitting";
        _series = new(name: $"AFIRMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _afirma = new Afirma(Period, Taps, Window);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _afirma!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _afirma.IsHot, ShowColdValues);
    }
}
