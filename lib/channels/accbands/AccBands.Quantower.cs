// AccBands.Quantower.cs - Quantower adapter for Acceleration Bands

using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// AccBands: Acceleration Bands - Quantower Indicator Adapter
/// Volatility-based channel indicator developed by Price Headley that creates
/// an adaptive price envelope around a moving average.
/// </summary>
public sealed class AccBandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Factor", sortIndex: 11, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 2)]
    public double Factor { get; set; } = 2.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private AccBands? _accBands;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"AccBands({Period},{Factor:F2})";

    public AccBandsIndicator()
    {
        Name = "AccBands - Acceleration Bands";
        Description = "Volatility-based adaptive price channel using SMA of High, Low, and Close";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _accBands = new AccBands(Period, Factor);

        // Middle line (SMA of close)
        AddLineSeries(new LineSeries("Middle", Volatility, 2, LineStyle.Solid));

        // Upper band
        AddLineSeries(new LineSeries("Upper", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));

        // Lower band
        AddLineSeries(new LineSeries("Lower", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_accBands == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        bool isNew = args.IsNewBar();

        TBar input = new(
            time: item.TimeLeft,
            open: item[PriceType.Open],
            high: item[PriceType.High],
            low: item[PriceType.Low],
            close: item[PriceType.Close],
            volume: item[PriceType.Volume]
        );

        _accBands.Update(input, isNew);

        bool isHot = _accBands.IsHot;

        // Middle line
        LinesSeries[0].SetValue(_accBands.Last.Value, isHot, ShowColdValues);

        // Upper band
        LinesSeries[1].SetValue(_accBands.Upper.Value, isHot, ShowColdValues);

        // Lower band
        LinesSeries[2].SetValue(_accBands.Lower.Value, isHot, ShowColdValues);
    }
}
