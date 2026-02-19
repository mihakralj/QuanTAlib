using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HtPhasorIndicator : Indicator, IWatchlistIndicator
{
    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private HtPhasor _htPhasor = null!;
    private readonly LineSeries _inPhaseSeries;
    private readonly LineSeries _quadratureSeries;
    private readonly LineSeries _zeroLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 32;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "HT_PHASOR";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/phasor/HtPhasor.Quantower.cs";

    public HtPhasorIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HT_PHASOR - Ehlers Hilbert Transform Phasor Components";
        Description = "Hilbert Transform Phasor components (InPhase, Quadrature) for cycle analysis";

        _inPhaseSeries = new LineSeries(name: "InPhase", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _quadratureSeries = new LineSeries(name: "Quadrature", color: Color.Orange, width: 1, style: LineStyle.Solid);
        _zeroLine = new LineSeries(name: "Zero", color: Color.Gray, width: 1, style: LineStyle.Dash);

        AddLineSeries(_inPhaseSeries);
        AddLineSeries(_quadratureSeries);
        AddLineSeries(_zeroLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _htPhasor = new HtPhasor();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar && args.Reason != UpdateReason.NewTick)
        {
            return;
        }
 
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        bool isNew = args.IsNewBar();
        TValue result = _htPhasor.Update(input, isNew);

        bool hot = _htPhasor.IsHot;
        _inPhaseSeries.SetValue(result.Value, hot, ShowColdValues);
        _quadratureSeries.SetValue(_htPhasor.Quadrature, hot, ShowColdValues);
        _zeroLine.SetValue(0.0);
    }
}
