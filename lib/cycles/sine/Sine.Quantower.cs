using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SineIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("HP Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int HpPeriod { get; set; } = 40;

    [InputParameter("SSF Period", sortIndex: 2, 1, 500, 1, 0)]
    public int SsfPeriod { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Sine _sine = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _zeroLine;
    private readonly LineSeries _upperLine;
    private readonly LineSeries _lowerLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SINE ({HpPeriod},{SsfPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/sine/Sine.Quantower.cs";

    public SineIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SINE - Ehlers Sine Wave";
        Description = "Ehlers' Sine Wave indicator extracts the dominant cycle from price data using High-Pass filter, Super-Smoother, and Hilbert Transform";

        _series = new LineSeries(name: "SINE", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _zeroLine = new LineSeries(name: "Zero", color: Color.Gray, width: 1, style: LineStyle.Dash);
        _upperLine = new LineSeries(name: "+1", color: Color.DarkGray, width: 1, style: LineStyle.Dot);
        _lowerLine = new LineSeries(name: "-1", color: Color.DarkGray, width: 1, style: LineStyle.Dot);
        AddLineSeries(_series);
        AddLineSeries(_zeroLine);
        AddLineSeries(_upperLine);
        AddLineSeries(_lowerLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _sine = new Sine(HpPeriod, SsfPeriod);
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
        TValue result = _sine.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _sine.IsHot, ShowColdValues);
        _zeroLine.SetValue(0.0);
        _upperLine.SetValue(1.0);
        _lowerLine.SetValue(-1.0);
    }
}