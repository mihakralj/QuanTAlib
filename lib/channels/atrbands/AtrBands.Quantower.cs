// AtrBands.Quantower.cs - Quantower adapter for ATR Bands

using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// AtrBands: ATR Bands - Quantower Indicator Adapter
/// Uses Average True Range (ATR) to create adaptive bands around a simple
/// moving average of the source price.
/// </summary>
public sealed class AtrBandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Multiplier", sortIndex: 11, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 2)]
    public double Multiplier { get; set; } = 2.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private AtrBands? _atrBands;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"AtrBands({Period},{Multiplier:F2})";

    public AtrBandsIndicator()
    {
        Name = "AtrBands - ATR Bands";
        Description = "ATR-based adaptive price channel using SMA middle line and RMA-smoothed True Range for band width";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _atrBands = new AtrBands(Period, Multiplier);

        // Middle line (SMA of close)
        AddLineSeries(new LineSeries("Middle", Volatility, 2, LineStyle.Solid));

        // Upper band
        AddLineSeries(new LineSeries("Upper", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));

        // Lower band
        AddLineSeries(new LineSeries("Lower", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_atrBands == null) return;

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

        _atrBands.Update(input, isNew);

        bool isHot = _atrBands.IsHot;

        // Middle line
        LinesSeries[0].SetValue(_atrBands.Last.Value, isHot, ShowColdValues);

        // Upper band
        LinesSeries[1].SetValue(_atrBands.Upper.Value, isHot, ShowColdValues);

        // Lower band
        LinesSeries[2].SetValue(_atrBands.Lower.Value, isHot, ShowColdValues);
    }
}