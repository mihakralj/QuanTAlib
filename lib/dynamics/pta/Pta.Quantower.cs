using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PtaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Long Period", sortIndex: 1, 3, 9999, 1, 0)]
    public int LongPeriod { get; set; } = 250;

    [InputParameter("Short Period", sortIndex: 2, 2, 9999, 1, 0)]
    public int ShortPeriod { get; set; } = 40;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pta _ind = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PTA {LongPeriod},{ShortPeriod}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/pta/Pta.Quantower.cs";

    public PtaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "PTA - Ehlers Precision Trend Analysis";
        Description = "Dual highpass filter bandpass for near-zero-lag trend extraction.";
        _series = new LineSeries(name: $"PTA {LongPeriod},{ShortPeriod}", color: Color.Red, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ind = new Pta(LongPeriod, ShortPeriod);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ind.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        _series.SetValue(result.Value, _ind.IsHot, ShowColdValues);
    }
}
