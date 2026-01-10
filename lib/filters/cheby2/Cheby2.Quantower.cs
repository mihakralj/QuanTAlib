using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Cheby2Indicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 3, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Attenuation (dB)", sortIndex: 2, 0.1, 100, 0.1, 2)]
    public double Attenuation { get; set; } = 5.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cheby2? _ma;
    private readonly LineSeries _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 5;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Cheby2 {Period}:{Attenuation}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/filters/cheby2/Cheby2.Quantower.cs";

    public Cheby2Indicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "Cheby2 - Chebyshev Type II Filter";
        Description = "Chebyshev Type II (Inverse Chebyshev) low-pass filter";
        _series = new LineSeries(name: $"Cheby2 {Period}", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _ma = new Cheby2(Period, Attenuation);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _ma!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series.SetValue(value, _ma.IsHot, ShowColdValues);
    }
}
