using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class FramaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period (even enforced)", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 16;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Frama ma = null!;
    protected LineSeries Series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"FRAMA {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_IIR/frama/Frama.Quantower.cs";

    public FramaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "FRAMA - Ehlers Fractal Adaptive Moving Average";
        Description = "Fractal Adaptive Moving Average using High/Low ranges and HL2 smoothing.";
        Series = new LineSeries(name: $"FRAMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Frama(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        var bar = new TBar(
            item.TimeLeft.Ticks,
            item[PriceType.Open],
            item[PriceType.High],
            item[PriceType.Low],
            item[PriceType.Close],
            item[PriceType.Volume]);
        TValue result = ma.Update(bar, isNew: args.IsNewBar());

        Series.SetValue(result.Value, ma.IsHot, ShowColdValues);
    }
}
