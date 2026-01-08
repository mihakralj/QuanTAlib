using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BpfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Max Period (HP)", sortIndex: 1, 1, 2000, 1, 0)]
    public int LowerPeriod { get; set; } = 40;

    [InputParameter("Min Period (LP)", sortIndex: 2, 1, 2000, 1, 0)]
    public int UpperPeriod { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Bpf? _bpf;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BPF {LowerPeriod}:{UpperPeriod}:{_sourceName}";

    public BpfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "BPF - Bandpass Filter";
        Description = "A BandPass Filter implemented as a cascade of HighPass and LowPass filters";
        _series = new(name: $"BPF {LowerPeriod}:{UpperPeriod}", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _bpf = new Bpf(LowerPeriod, UpperPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _bpf!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _bpf.IsHot, ShowColdValues);
    }
}