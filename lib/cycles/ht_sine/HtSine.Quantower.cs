using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HtSineIndicator : Indicator, IWatchlistIndicator
{
    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private HtSine _htSine = null!;
    private readonly LineSeries _sineSeries;
    private readonly LineSeries _leadSineSeries;
    private readonly LineSeries _zeroLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 63;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "HT_SINE";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ht_sine/HtSine.Quantower.cs";

    public HtSineIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HT_SINE - Hilbert Transform SineWave";
        Description = "Hilbert Transform SineWave indicator showing Sine and LeadSine for cycle timing";

        _sineSeries = new LineSeries(name: "Sine", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _leadSineSeries = new LineSeries(name: "LeadSine", color: Color.Orange, width: 1, style: LineStyle.Solid);
        _zeroLine = new LineSeries(name: "Zero", color: Color.Gray, width: 1, style: LineStyle.Dash);
        AddLineSeries(_sineSeries);
        AddLineSeries(_leadSineSeries);
        AddLineSeries(_zeroLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _htSine = new HtSine();
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
        TValue result = _htSine.Update(input, args.IsNewBar());

        _sineSeries.SetValue(result.Value, _htSine.IsHot, ShowColdValues);
        _leadSineSeries.SetValue(_htSine.LeadSine, _htSine.IsHot, ShowColdValues);
        _zeroLine.SetValue(0.0);
    }
}