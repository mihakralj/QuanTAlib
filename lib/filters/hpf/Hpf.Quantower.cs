using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class HpfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Length", sortIndex: 1, 2, 2000, 1, 0)]
    public int Length { get; set; } = 40;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hpf? _hpf;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HPF {Length}:{_sourceName}";

    public HpfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "HPF - Highpass Filter (2-Pole)";
        Description = "2-Pole Infinite Impulse Response (IIR) highpass filter.";
        _series = new(name: $"HPF {Length}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _hpf = new Hpf(Length);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _hpf!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _hpf.IsHot, ShowColdValues);
    }
}