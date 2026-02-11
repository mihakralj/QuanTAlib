using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// MOM (Momentum) Quantower indicator.
/// Calculates absolute price change over a lookback period.
/// Formula: current - past
/// </summary>
public class MomIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period { get; set; } = 10;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Mom? _mom;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period + 1;
    public override string ShortName => $"MOM({Period})";

    public MomIndicator()
    {
        Name = "MOM - Momentum";
        Description = "Calculates absolute price change: current - past";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _mom = new Mom(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("MOM", IndicatorExtensions.Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_mom == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _mom.Update(input, isNew);

        bool isHot = _mom.IsHot;

        LinesSeries[0].SetValue(_mom.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double mom = _mom.Last.Value;
            Color color;
            if (mom > 0)
            {
                color = Color.Green;
            }
            else if (mom < 0)
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
