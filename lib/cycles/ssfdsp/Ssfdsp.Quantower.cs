using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SsfdspIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 4, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ssfdsp _ssfdsp = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _zeroLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SSFDSP ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ssfdsp/Ssfdsp.Quantower.cs";

    public SsfdspIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SSFDSP - SSF Detrended Synthetic Price";
        Description = "Ehlers' Super Smooth Filter based Detrended Synthetic Price oscillator for cycle extraction";

        _series = new LineSeries(name: "SSFDSP", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _zeroLine = new LineSeries(name: "Zero", color: Color.Gray, width: 1, style: LineStyle.Dash);
        AddLineSeries(_series);
        AddLineSeries(_zeroLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ssfdsp = new Ssfdsp(Period);
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
        TValue result = _ssfdsp.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _ssfdsp.IsHot, ShowColdValues);
        _zeroLine.SetValue(0.0);
    }
}