using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// JBANDS: Jurik Adaptive Envelope Bands - Quantower Indicator Adapter
/// Upper and Lower bands from JMA's internal adaptive envelope tracking.
/// These bands snap to new extremes instantly but decay smoothly toward price.
/// Middle band is the JMA smoothed value itself.
/// </summary>
public sealed class JbandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 7;

    [InputParameter("Phase", sortIndex: 20, minimum: -100, maximum: 100, increment: 1, decimalPlaces: 0)]
    public int Phase { get; set; } = 0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Jbands? _indicator;

    public int MinHistoryDepths => (int)Math.Ceiling(20.0 + (80.0 * Math.Pow(Period, 0.36)));
    public override string ShortName => $"Jbands({Period},{Phase})";

    public JbandsIndicator()
    {
        Name = "Jbands - Jurik Adaptive Envelope Bands";
        Description = "Adaptive volatility bands from JMA's internal envelope tracking with snap-and-decay behavior";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new Jbands(Period, Phase);

        AddLineSeries(new LineSeries("Middle", Color.DodgerBlue, 2, LineStyle.Solid));
        AddLineSeries(new LineSeries("Upper", Color.FromArgb(255, 180, 180), 1, LineStyle.Dash));
        AddLineSeries(new LineSeries("Lower", Color.FromArgb(180, 180, 255), 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_indicator is null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        bool isNew = args.IsNewBar();

        TValue input = new(
            time: item.TimeLeft,
            value: item[PriceType.Close]
        );

        _indicator.Update(input, isNew);

        bool isHot = _indicator.IsHot;

        LinesSeries[0].SetValue(_indicator.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(_indicator.Upper.Value, isHot, ShowColdValues);
        LinesSeries[2].SetValue(_indicator.Lower.Value, isHot, ShowColdValues);
    }
}
