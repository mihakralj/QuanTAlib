using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Ssf2Indicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ssf2 _ssf = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SSF2 {Period}:{_sourceName}";

    public Ssf2Indicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "SSF2 - Ehlers 2-Pole Super Smoother Filter";
        Description = "Ehlers 2-Pole Super Smoother Filter: 2-pole Butterworth lowpass with maximally flat passband response";
        _series = new LineSeries(name: $"SSF2 {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ssf = new Ssf2(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _ssf.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _series.SetValue(value, _ssf.IsHot, ShowColdValues);
    }
}
