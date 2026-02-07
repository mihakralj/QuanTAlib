using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// ROCR (Rate of Change Ratio) Quantower indicator.
/// Calculates price ratio over a lookback period.
/// Formula: current / past (ratio around 1.0)
/// </summary>
public class RocrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period { get; set; } = 9;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Rocr? _rocr;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period + 1;
    public override string ShortName => $"ROCR({Period})";

    public RocrIndicator()
    {
        Name = "ROCR - Rate of Change Ratio";
        Description = "Calculates price ratio: current / past (ratio around 1.0)";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _rocr = new Rocr(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("ROCR", IndicatorExtensions.Momentum, 2, LineStyle.Solid));
        AddLineSeries(new LineSeries("One", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_rocr == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _rocr.Update(input, isNew);

        bool isHot = _rocr.IsHot;

        LinesSeries[0].SetValue(_rocr.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(1.0);

        if (isHot || ShowColdValues)
        {
            double rocr = _rocr.Last.Value;
            Color color;
            if (rocr > 1.0)
            {
                color = Color.Green;
            }
            else if (rocr < 1.0)
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
