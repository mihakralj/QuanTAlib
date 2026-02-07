using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// ROCP (Rate of Change Percentage) Quantower indicator.
/// Calculates percentage price change over a lookback period.
/// Formula: 100 × (current - past) / past
/// </summary>
public class RocpIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period { get; set; } = 9;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Rocp? _rocp;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period + 1;
    public override string ShortName => $"ROCP({Period})";

    public RocpIndicator()
    {
        Name = "ROCP - Rate of Change Percentage";
        Description = "Calculates percentage price change: 100 × (current - past) / past";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _rocp = new Rocp(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("ROCP", IndicatorExtensions.Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_rocp == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _rocp.Update(input, isNew);

        bool isHot = _rocp.IsHot;

        LinesSeries[0].SetValue(_rocp.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double rocp = _rocp.Last.Value;
            Color color;
            if (rocp > 0)
            {
                color = Color.Green;
            }
            else if (rocp < 0)
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
