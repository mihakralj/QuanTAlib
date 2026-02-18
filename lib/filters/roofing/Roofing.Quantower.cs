using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RoofingIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("HP Length", sortIndex: 1, 1, 2000, 1, 0)]
    public int HpLength { get; set; } = 48;

    [InputParameter("SS Length", sortIndex: 2, 1, 2000, 1, 0)]
    public int SsLength { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Roofing _roofing = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ROOFING {HpLength}:{SsLength}:{_sourceName}";

    public RoofingIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ROOFING - Ehlers Roofing Filter";
        Description = "Ehlers Roofing Filter: bandpass filter cascading a 2nd-order Butterworth Highpass with a Super Smoother";
        _series = new LineSeries(name: $"ROOFING {HpLength}:{SsLength}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _roofing = new Roofing(HpLength, SsLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _roofing.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _series.SetValue(value, _roofing.IsHot, ShowColdValues);
    }
}
