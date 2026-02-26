using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DoscIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("RSI Period", sortIndex: 1, 1, 500, 1, 0)]
    public int RsiPeriod { get; set; } = 14;

    [InputParameter("EMA1 Period", sortIndex: 2, 1, 500, 1, 0)]
    public int Ema1Period { get; set; } = 5;

    [InputParameter("EMA2 Period", sortIndex: 3, 1, 500, 1, 0)]
    public int Ema2Period { get; set; } = 3;

    [InputParameter("Signal Period", sortIndex: 4, 1, 500, 1, 0)]
    public int SigPeriod { get; set; } = 9;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dosc _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DOSC {RsiPeriod},{Ema1Period},{Ema2Period},{SigPeriod}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/dosc/Dosc.Quantower.cs";

    public DoscIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "DOSC - Derivative Oscillator";
        Description = "Four-stage pipeline: Wilder RSI → EMA1 → EMA2 (double-smooth) → SMA signal. DOSC = EMA2 - Signal.";
        _series = new LineSeries(name: $"DOSC", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ma = new Dosc(RsiPeriod, Ema1Period, Ema2Period, SigPeriod);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        _series.SetValue(result.Value, _ma.IsHot, ShowColdValues);
    }
}
