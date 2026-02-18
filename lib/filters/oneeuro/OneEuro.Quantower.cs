using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class OneEuroIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Min Cutoff Frequency", sortIndex: 1, 0.001, 100.0, 0.1, 3)]
    public double MinCutoff { get; set; } = 1.0;

    [InputParameter("Speed Coefficient (β)", sortIndex: 2, 0.0, 10.0, 0.001, 4)]
    public double Beta { get; set; } = 0.007;

    [InputParameter("Derivative Cutoff", sortIndex: 3, 0.001, 100.0, 0.1, 3)]
    public double DCutoff { get; set; } = 1.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private OneEuro _oe = null!;
    private readonly LineSeries _oeSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"1€ {MinCutoff}:{Beta}:{_sourceName}";

    public OneEuroIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "ONEEURO - One Euro Filter";
        Description = "Speed-adaptive low-pass filter for jitter removal with low lag";
        _oeSeries = new LineSeries(name: $"1€ {MinCutoff}:{Beta}", color: Color.Teal, width: 2, style: LineStyle.Solid);
        AddLineSeries(_oeSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _oe = new OneEuro(MinCutoff, Beta, DCutoff);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _oe.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _oeSeries.SetValue(value, _oe.IsHot, ShowColdValues);
    }
}
