using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HannIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Length", sortIndex: 1, 2, 2000, 1, 0)]
    public int Length { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hann _hann = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Hann {Length}:{_sourceName}";

    public HannIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "Hann - Hann Filter";
        Description = "Hann FIR Filter";
        _series = new LineSeries(name: $"Hann {Length}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _hann = new Hann(Length);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _hann.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _series.SetValue(value, _hann.IsHot, ShowColdValues);
    }
}
