using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EbswIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("HP Length", sortIndex: 1, 1, 2000, 1, 0)]
    public int HpLength { get; set; } = 40;

    [InputParameter("SSF Length", sortIndex: 2, 1, 500, 1, 0)]
    public int SsfLength { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ebsw _ebsw = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _zeroLine;
    private readonly LineSeries _upperLine;
    private readonly LineSeries _lowerLine;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"EBSW ({HpLength},{SsfLength})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ebsw/Ebsw.Quantower.cs";

    public EbswIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "EBSW - Ehlers Even Better Sinewave";
        Description = "Ehlers' Even Better Sinewave oscillator with high-pass filter, super-smoother, and automatic gain control";

        _series = new LineSeries(name: "EBSW", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
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
        _ebsw = new Ebsw(HpLength, SsfLength);
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
        TValue result = _ebsw.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _ebsw.IsHot, ShowColdValues);
        _zeroLine.SetValue(0.0);
        _upperLine.SetValue(1.0);
        _lowerLine.SetValue(-1.0);
    }
}