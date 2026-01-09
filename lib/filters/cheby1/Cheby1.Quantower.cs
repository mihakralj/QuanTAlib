using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Cheby1Indicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 3, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Ripple (dB)", sortIndex: 2, 0.001, 100, 0.1, 2)]
    public double Ripple { get; set; } = 0.5;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cheby1? _ma;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 5;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Cheby1 {Period}:{Ripple}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/filters/cheby1/Cheby1.Quantower.cs";

    public Cheby1Indicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "Cheby1 - Chebyshev Type I Filter";
        Description = "Chebyshev Type I low-pass filter with passband ripple";
        _series = new(name: $"Cheby1 {Period}", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ma = new Cheby1(Period, Ripple);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _ma!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _ma.IsHot, ShowColdValues);
    }
}
