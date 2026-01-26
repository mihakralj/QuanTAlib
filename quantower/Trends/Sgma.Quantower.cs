// Sgma.Quantower.cs - Quantower adapter for Savitzky-Golay Moving Average

using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// SGMA: Savitzky-Golay Moving Average - Quantower Indicator Adapter
/// A FIR filter that uses polynomial fitting to smooth data while preserving
/// higher moments (peaks, valleys, and inflection points).
/// </summary>
public sealed class SgmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 3, maximum: 500, increment: 2, decimalPlaces: 0)]
    public int Period { get; set; } = 9;

    [InputParameter("Polynomial Degree", sortIndex: 11, minimum: 0, maximum: 4, increment: 1, decimalPlaces: 0)]
    public int Degree { get; set; } = 2;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Sgma? _sgma;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"SGMA({Period},{Degree})";

    public SgmaIndicator()
    {
        Name = "SGMA - Savitzky-Golay Moving Average";
        Description = "A FIR filter using polynomial fitting for smoothing with shape preservation.";
        SeparateWindow = false;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _sgma = new Sgma(Period, Degree);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("SGMA", Averages, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_sgma == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        var result = _sgma.Update(input, isNew);

        bool isHot = _sgma.IsHot;
        LinesSeries[0].SetValue(result.Value, isHot, ShowColdValues);
    }
}
