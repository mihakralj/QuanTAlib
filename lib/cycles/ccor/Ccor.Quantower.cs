using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CcorIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 200, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Threshold", sortIndex: 2, 0.1, 90.0, 0.1, 1)]
    public double Threshold { get; set; } = 9.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ccor _ccor = null!;
    private readonly LineSeries _realSeries;
    private readonly LineSeries _imagSeries;
    private readonly LineSeries _angleSeries;
    private readonly LineSeries _stateSeries;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CCOR ({Period},{Threshold:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ccor/Ccor.Quantower.cs";

    public CcorIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CCOR - Ehlers Correlation Cycle";
        Description = "Ehlers' Correlation Cycle uses dual Pearson correlation (cosine + negative sine) to derive a phasor, monotonic angle, and market state classification";

        _realSeries = new LineSeries(name: "Real", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _imagSeries = new LineSeries(name: "Imag", color: Color.FromArgb(128, 128, 255), width: 1, style: LineStyle.Dash);
        _angleSeries = new LineSeries(name: "Angle", color: Color.FromArgb(200, 200, 100), width: 1, style: LineStyle.Dot);
        _stateSeries = new LineSeries(name: "State", color: Color.FromArgb(255, 165, 0), width: 2, style: LineStyle.Histogramm);
        AddLineSeries(_realSeries);
        AddLineSeries(_imagSeries);
        AddLineSeries(_angleSeries);
        AddLineSeries(_stateSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ccor = new Ccor(Period, Threshold);
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
        TValue result = _ccor.Update(input, args.IsNewBar());

        _realSeries.SetValue(result.Value, _ccor.IsHot, ShowColdValues);
        _imagSeries.SetValue(_ccor.Imag, _ccor.IsHot, ShowColdValues);
        _angleSeries.SetValue(_ccor.Angle, _ccor.IsHot, ShowColdValues);
        _stateSeries.SetValue(_ccor.MarketState, _ccor.IsHot, ShowColdValues);
    }
}
