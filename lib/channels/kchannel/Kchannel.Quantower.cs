using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// Kchannel: Keltner Channel - Quantower Indicator Adapter
/// A volatility-based envelope using EMA as the middle line and ATR for band width.
/// Middle = EMA(close, period) with warmup compensation
/// Upper = Middle + (multiplier × ATR)
/// Lower = Middle - (multiplier × ATR)
/// ATR uses RMA (Wilder's smoothing) with warmup compensation.
/// </summary>
public sealed class KchannelIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 20, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 1)]
    public double Multiplier { get; set; } = 2.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Kchannel? _indicator;

    public int MinHistoryDepths => Period * 2;
    public override string ShortName => $"Kchannel({Period},{Multiplier})";

    public KchannelIndicator()
    {
        Name = "Kchannel - Keltner Channel";
        Description = "EMA-based channel with ATR-derived band width";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new Kchannel(Period, Multiplier);

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

        TBar input = new(
            time: item.TimeLeft,
            open: item[PriceType.Open],
            high: item[PriceType.High],
            low: item[PriceType.Low],
            close: item[PriceType.Close],
            volume: item[PriceType.Volume]
        );

        _indicator.Update(input, isNew);

        bool isHot = _indicator.IsHot;

        LinesSeries[0].SetValue(_indicator.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(_indicator.Upper.Value, isHot, ShowColdValues);
        LinesSeries[2].SetValue(_indicator.Lower.Value, isHot, ShowColdValues);
    }
}
