using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VossIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Predict", sortIndex: 2, 1, 100, 1, 0)]
    public int Predict { get; set; } = 3;

    [InputParameter("Bandwidth", sortIndex: 3, 0.01, 0.99, 0.01, 2)]
    public double Bandwidth { get; set; } = 0.25;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Voss _voss = null!;
    private readonly LineSeries _vossSeries;
    private readonly LineSeries _filtSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VOSS {Period}:{Predict}:{Bandwidth:F2}:{_sourceName}";

    public VossIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VOSS - Ehlers Voss Predictive Filter";
        Description = "Ehlers Voss Predictive Filter: two-pole bandpass + weighted feedback predictor with negative group delay";
        _vossSeries = new LineSeries(name: $"Voss {Period}:{Predict}", color: Color.DodgerBlue, width: 2, style: LineStyle.Solid);
        _filtSeries = new LineSeries(name: $"Filt {Period}:{Predict}", color: Color.Red, width: 1, style: LineStyle.Solid);
        AddLineSeries(_vossSeries);
        AddLineSeries(_filtSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _voss = new Voss(Period, Predict, Bandwidth);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _voss.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _vossSeries.SetValue(value, _voss.IsHot, ShowColdValues);
        _filtSeries.SetValue(_voss.LastFilt, _voss.IsHot, ShowColdValues);
    }
}
