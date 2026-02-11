using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class QstickIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("MA Type", sortIndex: 1, variants: new object[] { "SMA", "SMA", "EMA", "EMA" })]
    public string MaType { get; set; } = "SMA";

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    public override string ShortName => $"QSTICK({Period},{MaType})";

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    private Qstick _indicator = null!;
    private readonly LineSeries _series;

    public QstickIndicator()
    {
        Name = "Qstick Indicator";
        Description = "Measures average candlestick body direction by calculating the moving average of close minus open.";
        _series = new LineSeries("Qstick", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
        SeparateWindow = true;
    }

    protected override void OnInit()
    {
        bool useEma = string.Equals(MaType, "EMA", StringComparison.Ordinal);
        _indicator = new Qstick(Period, useEma);
        AddLineLevel(0, "Zero", Color.Gray, 1, LineStyle.Dash);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var bar = this.GetInputBar(args);
        var result = _indicator.Update(bar, isNew);
        _series.SetValue(result.Value, _indicator.IsHot, ShowColdValues);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
    }
}
