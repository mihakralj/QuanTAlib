using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HtDcperiodIndicator : Indicator, IWatchlistIndicator
{
    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private HtDcperiod _htDcperiod = null!;
    private readonly LineSeries _periodSeries;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 32;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "HT_DCPERIOD";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ht_dcperiod/HtDcperiod.Quantower.cs";

    public HtDcperiodIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HT_DCPERIOD - Hilbert Transform Dominant Cycle Period";
        Description = "Hilbert Transform Dominant Cycle Period indicator measuring the dominant cycle period in price data";

        _periodSeries = new LineSeries(name: "DCPeriod", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        AddLineSeries(_periodSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _htDcperiod = new HtDcperiod();
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
        TValue result = _htDcperiod.Update(input, args.IsNewBar());

        _periodSeries.SetValue(result.Value, _htDcperiod.IsHot, ShowColdValues);
    }
}
