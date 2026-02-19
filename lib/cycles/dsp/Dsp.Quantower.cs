using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DspIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 4, 2000, 1, 0)]
    public int Period { get; set; } = 40;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dsp _dsp = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _zeroLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DSP ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/dsp/Dsp.Quantower.cs";

    public DspIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DSP - Ehlers Detrended Synthetic Price";
        Description = "Ehlers' Detrended Synthetic Price oscillator removes trend using dual EMA smoothing";

        _series = new LineSeries(name: "DSP", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _zeroLine = new LineSeries(name: "Zero", color: Color.Gray, width: 1, style: LineStyle.Dash);
        AddLineSeries(_series);
        AddLineSeries(_zeroLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dsp = new Dsp(Period);
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
        TValue result = _dsp.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _dsp.IsHot, ShowColdValues);
        _zeroLine.SetValue(0.0);
    }
}