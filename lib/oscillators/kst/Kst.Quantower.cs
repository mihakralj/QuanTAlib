using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class KstIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("ROC Period 1", sortIndex: 1, 1, 500, 1, 0)]
    public int R1 { get; set; } = 10;

    [InputParameter("ROC Period 2", sortIndex: 2, 1, 500, 1, 0)]
    public int R2 { get; set; } = 15;

    [InputParameter("ROC Period 3", sortIndex: 3, 1, 500, 1, 0)]
    public int R3 { get; set; } = 20;

    [InputParameter("ROC Period 4", sortIndex: 4, 1, 500, 1, 0)]
    public int R4 { get; set; } = 30;

    [InputParameter("SMA Smooth 1", sortIndex: 5, 1, 500, 1, 0)]
    public int S1 { get; set; } = 10;

    [InputParameter("SMA Smooth 2", sortIndex: 6, 1, 500, 1, 0)]
    public int S2 { get; set; } = 10;

    [InputParameter("SMA Smooth 3", sortIndex: 7, 1, 500, 1, 0)]
    public int S3 { get; set; } = 10;

    [InputParameter("SMA Smooth 4", sortIndex: 8, 1, 500, 1, 0)]
    public int S4 { get; set; } = 15;

    [InputParameter("Signal Period", sortIndex: 9, 1, 500, 1, 0)]
    public int SignalPeriod { get; set; } = 9;

    [IndicatorExtensions.DataSourceInput(sortIndex: 10)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Kst _kst = null!;
    private readonly LineSeries _kstSeries;
    private readonly LineSeries _signalSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"KST ({R1},{R2},{R3},{R4})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/kst/Kst.Quantower.cs";

    public KstIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "KST - Know Sure Thing Oscillator";
        Description = "Weighted sum of 4 smoothed ROC values with signal line (SMA of KST)";

        _kstSeries = new LineSeries(name: "KST", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Aqua, width: 1, style: LineStyle.Solid);

        AddLineSeries(_kstSeries);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _kst = new Kst(R1, R2, R3, R4, S1, S2, S3, S4, SignalPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        _ = _kst.Update(new TValue(item.TimeLeft, price), args.IsNewBar());

        _kstSeries.SetValue(_kst.KstValue.Value, _kst.IsHot, ShowColdValues);
        _signalSeries.SetValue(_kst.Signal.Value, _kst.IsHot, ShowColdValues);
    }
}
