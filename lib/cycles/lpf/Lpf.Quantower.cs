using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class LpfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Lower Bound", sortIndex: 1, 8, 200, 1, 0)]
    public int LowerBound { get; set; } = 18;

    [InputParameter("Upper Bound", sortIndex: 2, 10, 500, 1, 0)]
    public int UpperBound { get; set; } = 40;

    [InputParameter("Data Length", sortIndex: 3, 4, 200, 1, 0)]
    public int DataLength { get; set; } = 40;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Lpf _lpf = null!;
    private readonly LineSeries _cycleSeries;
    private readonly LineSeries _signalSeries;
    private readonly LineSeries _predictSeries;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LPF ({LowerBound},{UpperBound},{DataLength})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/lpf/Lpf.Quantower.cs";

    public LpfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "LPF - Ehlers Linear Predictive Filter";
        Description = "Ehlers' Linear Predictive Filter estimates the dominant cycle period using Griffiths adaptive coefficients and spectral analysis";

        _cycleSeries = new LineSeries(name: "Cycle", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Lime, width: 1, style: LineStyle.Solid);
        _predictSeries = new LineSeries(name: "Predict", color: Color.Red, width: 1, style: LineStyle.Dot);
        AddLineSeries(_cycleSeries);
        AddLineSeries(_signalSeries);
        AddLineSeries(_predictSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _lpf = new Lpf(LowerBound, UpperBound, DataLength);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
        {
            return;
        }

        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        TValue result = _lpf.Update(input, args.IsNewBar());

        _cycleSeries.SetValue(result.Value, _lpf.IsHot, ShowColdValues);
        _signalSeries.SetValue(_lpf.Signal * UpperBound * 0.5 + (LowerBound + UpperBound) * 0.5, _lpf.IsHot, ShowColdValues);
        _predictSeries.SetValue(_lpf.Predict * UpperBound * 0.5 + (LowerBound + UpperBound) * 0.5, _lpf.IsHot, ShowColdValues);
    }
}
