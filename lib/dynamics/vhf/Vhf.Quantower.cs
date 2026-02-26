using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VhfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 200, 1, 0)]
    public int Period { get; set; } = 28;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vhf _vhf = null!;
    private readonly LineSeries _vhfSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VHF {Period}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/vhf/Vhf.Quantower.cs";

    public VhfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VHF - Vertical Horizontal Filter";
        Description = "Measures trend strength via (Highest - Lowest) / Sum(|bar-to-bar changes|)";

        _vhfSeries = new LineSeries(name: "VHF", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_vhfSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _vhf = new Vhf(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _vhf.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _vhfSeries.SetValue(value, _vhf.IsHot, ShowColdValues);
    }
}
