using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 7;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pma _pma = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _triggerSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PMA {Period}:{_sourceName}";
public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_FIR/pma/Pma.Quantower.cs";

    public PmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        _sourceName = Source.ToString();
        Name = "PMA - Predictive Moving Average";
        Description = "Ehlers Predictive Moving Average";
        _series = new LineSeries(name: $"PMA {Period}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _triggerSeries = new LineSeries(name: "Trigger", color: Color.Orange, width: 1, style: LineStyle.Solid);
        AddLineSeries(_series);
        AddLineSeries(_triggerSeries);
    }

    protected override void OnInit()
    {
        _pma = new Pma(Period);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        TValue result = _pma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());

        _series.SetValue(result.Value, _pma.IsHot, ShowColdValues);
        _triggerSeries.SetValue(_pma.Trigger.Value, _pma.IsHot, ShowColdValues);
    }
}
