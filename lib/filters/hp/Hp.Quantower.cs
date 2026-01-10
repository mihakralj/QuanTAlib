using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class HpIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Smoothing (Lambda)", sortIndex: 1, 0.1, 100000, 10, 1)]
    public double Lambda { get; set; } = 1600;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hp? _hp;
    private readonly LineSeries? _series;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HP {Lambda}:{_sourceName}";

    public HpIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "HP - Hodrick-Prescott Filter";
        Description = "Causal Hodrick-Prescott Filter";
        _series = new LineSeries(name: $"HP {Lambda}", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _hp = new Hp(Lambda);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _hp!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _hp.IsHot, ShowColdValues);
    }
}