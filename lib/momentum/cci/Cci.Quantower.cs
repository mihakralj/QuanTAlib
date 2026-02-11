using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CciIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 2, maximum: 200)]
    public int Period { get; set; } = 20;

    public int MinHistoryDepths => Period;

    private Cci? _cci;
    private readonly LineSeries _series;

    public CciIndicator()
    {
        Name = "CCI";
        Description = "Commodity Channel Index - momentum oscillator measuring price deviation from mean";
        SeparateWindow = true;

        _series = new LineSeries("CCI", Color.Yellow, 2, LineStyle.Solid);
    }

    protected override void OnInit()
    {
        _cci = new Cci(Period);
        AddLineSeries(_series);
        AddLineLevel(100, "Overbought", Color.Red, 1, LineStyle.Dash);
        AddLineLevel(-100, "Oversold", Color.Green, 1, LineStyle.Dash);
        AddLineLevel(0, "Zero", Color.Gray, 1, LineStyle.Dot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (_cci == null)
        {
            return;
        }

        TBar bar = this.GetInputBar(args);
        bool isNew = args.Reason != UpdateReason.HistoricalBar || HistoricalData.Count == 1;
        var result = _cci.Update(bar, isNew);
        _series.SetValue(result.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        if (_cci == null)
        {
            return;
        }
        this.PaintSmoothCurve(args, _series, _cci.Period, showColdValues: true, tension: 0.5);
    }
}
