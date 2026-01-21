using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// Decaychannel: Decay Min-Max Channel - Quantower Indicator Adapter
/// Tracks highest high and lowest low with exponential decay toward midpoint.
/// Uses ln(2)/period for true half-life behavior.
/// </summary>
public sealed class DecaychannelIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 100;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Decaychannel? _indicator;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"Decaychannel({Period})";

    public DecaychannelIndicator()
    {
        Name = "Decaychannel - Decay Min-Max Channel";
        Description = "Adaptive channel with exponential decay toward midpoint using half-life behavior";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new Decaychannel(Period);

        AddLineSeries(new LineSeries("Middle", Color.DodgerBlue, 2, LineStyle.Solid));
        AddLineSeries(new LineSeries("Upper", Color.FromArgb(255, 180, 180), 1, LineStyle.Dash));
        AddLineSeries(new LineSeries("Lower", Color.FromArgb(180, 180, 255), 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_indicator is null)
            return;

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
