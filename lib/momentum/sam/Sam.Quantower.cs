using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// SAM (Smoothed Adaptive Momentum) Quantower indicator.
/// Ehlers adaptive momentum oscillator that measures price change over the
/// dominant cycle period, then smooths with a 2-pole Super Smoother filter.
/// </summary>
public class SamIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Alpha", 0, 0.01, 1.0, 0.01, 2)]
    public double Alpha { get; set; } = 0.07;

    [InputParameter("Cutoff", 1, 2, 100, 1, 0)]
    public int Cutoff { get; set; } = 8;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Sam? _sam;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 100; // WarmupPeriod = MaxCyclePeriod * 2
    public override string ShortName => $"SAM({Alpha},{Cutoff})";

    public SamIndicator()
    {
        Name = "SAM - Smoothed Adaptive Momentum";
        Description = "Ehlers adaptive momentum oscillator using Hilbert Transform cycle detection and Super Smoother";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _sam = new Sam(Alpha, Cutoff);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("SAM", IndicatorExtensions.Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_sam == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _sam.Update(input, isNew);

        bool isHot = _sam.IsHot;

        LinesSeries[0].SetValue(_sam.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double sam = _sam.Last.Value;
            Color color;
            if (sam > 0)
            {
                color = Color.Green;
            }
            else if (sam < 0)
            {
                color = Color.Red;
            }
            else
            {
                color = Color.Gray;
            }

            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(color));
        }
    }
}
