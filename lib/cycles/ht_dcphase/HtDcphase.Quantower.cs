using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HtDcphaseIndicator : Indicator, IWatchlistIndicator
{
    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private HtDcphase _htDcphase = null!;
    private readonly LineSeries _phaseSeries;
    private readonly LineSeries _zeroLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 63;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "HT_DCPHASE";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ht_dcphase/HtDcphase.Quantower.cs";

    public HtDcphaseIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HT_DCPHASE - Ehlers Hilbert Transform Dominant Cycle Phase";
        Description = "Hilbert Transform Dominant Cycle Phase indicator measuring the phase angle of the dominant cycle in price data (degrees, -45 to 315)";

        _phaseSeries = new LineSeries(name: "DCPhase", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _zeroLine = new LineSeries(name: "Zero", color: Color.Gray, width: 1, style: LineStyle.Dash);

        AddLineSeries(_phaseSeries);
        AddLineSeries(_zeroLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _htDcphase = new HtDcphase();
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
        TValue result = _htDcphase.Update(input, args.IsNewBar());

        _phaseSeries.SetValue(result.Value, _htDcphase.IsHot, ShowColdValues);
        _zeroLine.SetValue(0.0);
    }
}
