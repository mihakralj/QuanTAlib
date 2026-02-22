using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CcycIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Alpha", sortIndex: 1, 0.01, 0.99, 0.01, 2)]
    public double Alpha { get; set; } = 0.07;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ccyc _ccyc = null!;
    private readonly LineSeries _cycleSeries;
    private readonly LineSeries _triggerSeries;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CCYC ({Alpha:F2})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ccyc/Ccyc.Quantower.cs";

    public CcycIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CCYC - Ehlers Cyber Cycle";
        Description = "Ehlers' Cyber Cycle isolates the dominant cycle component using a 4-tap FIR pre-smoother and a 2-pole high-pass IIR filter";

        _cycleSeries = new LineSeries(name: "Cycle", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _triggerSeries = new LineSeries(name: "Trigger", color: Color.FromArgb(128, 128, 255), width: 1, style: LineStyle.Dash);
        AddLineSeries(_cycleSeries);
        AddLineSeries(_triggerSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ccyc = new Ccyc(Alpha);
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
        TValue result = _ccyc.Update(input, args.IsNewBar());

        _cycleSeries.SetValue(result.Value, _ccyc.IsHot, ShowColdValues);
        _triggerSeries.SetValue(_ccyc.Trigger, _ccyc.IsHot, ShowColdValues);
    }
}
