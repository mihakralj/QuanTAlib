using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class StcIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Cycle Length", sortIndex: 1, 2, 2000, 1, 0)]
    public int CycleLength { get; set; } = 12;

    [InputParameter("Fast Length", sortIndex: 2, 2, 2000, 1, 0)]
    public int FastLength { get; set; } = 26;

    [InputParameter("Slow Length", sortIndex: 3, 2, 2000, 1, 0)]
    public int SlowLength { get; set; } = 50;

    [InputParameter("Smoothing", sortIndex: 4, variants: new object[] {
        "None", StcSmoothing.None,
        "EMA", StcSmoothing.Ema,
        "Sigmoid", StcSmoothing.Sigmoid,
        "Digital", StcSmoothing.Digital,
    })]
    public StcSmoothing Smoothing { get; set; } = StcSmoothing.Sigmoid;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Stc? _stc;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"STC {CycleLength}:{FastLength}:{SlowLength}:{Smoothing}:{_sourceName}";

    public StcIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "STC - Schaff Trend Cycle";
        Description = "Schaff Trend Cycle Oscillator";
        _series = new(name: "STC", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _stc = new Stc(kPeriod: CycleLength, dPeriod: CycleLength, fastLength: FastLength, slowLength: SlowLength, smoothing: Smoothing);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _stc!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _stc.IsHot, ShowColdValues);
    }
}