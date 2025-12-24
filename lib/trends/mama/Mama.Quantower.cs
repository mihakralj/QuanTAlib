using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MamaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Limit", sortIndex: 1, 0.01, 0.99, 0.01, 2)]
    public double FastLimit { get; set; } = 0.5;

    [InputParameter("Slow Limit", sortIndex: 2, 0.01, 0.99, 0.01, 2)]
    public double SlowLimit { get; set; } = 0.05;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Mama? _mama;
    private readonly LineSeries? _series;
    private readonly LineSeries? _famaSeries;
    private string? _sourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MAMA:{_sourceName}";

    public MamaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "MAMA - MESA Adaptive Moving Average";
        Description = "MESA Adaptive Moving Average";
        _series = new(name: "MAMA", color: Color.Orange, width: 2, style: LineStyle.Solid);
        _famaSeries = new(name: "FAMA", color: Color.Red, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
        AddLineSeries(_famaSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _mama = new Mama(FastLimit, SlowLimit);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _mama!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew).Value;
        _series!.SetValue(value, _mama.IsHot, ShowColdValues);
        _famaSeries!.SetValue(_mama.Fama.Value, _mama.IsHot, ShowColdValues);
    }
}
