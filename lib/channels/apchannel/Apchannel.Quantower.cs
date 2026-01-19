// Apchannel.Quantower.cs - Quantower adapter for Adaptive Price Channel

using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// APCHANNEL: Adaptive Price Channel - Quantower Indicator Adapter
/// An adaptive channel using exponential moving averages of highs and lows
/// with configurable smoothing factor (alpha).
/// </summary>
public sealed class ApchannelIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Alpha", sortIndex: 10, minimum: 0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double Alpha { get; set; } = 0.2;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Apchannel? _apchannel;

    public int MinHistoryDepths => (int)Math.Ceiling(3.0 / Alpha);
    public override string ShortName => $"Apchannel({Alpha:F2})";

    public ApchannelIndicator()
    {
        Name = "Apchannel - Adaptive Price Channel";
        Description = "Adaptive channel using exponential moving averages of highs and lows";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _apchannel = new Apchannel(Alpha);

        // Middle line (average of upper and lower)
        AddLineSeries(new LineSeries("Middle", Volatility, 2, LineStyle.Solid));

        // Upper band (EMA of highs)
        AddLineSeries(new LineSeries("Upper", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));

        // Lower band (EMA of lows)
        AddLineSeries(new LineSeries("Lower", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_apchannel == null) return;

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

        _apchannel.Update(input, isNew);

        bool isHot = _apchannel.IsHot;

        // Middle line (Last.Value is the midpoint)
        LinesSeries[0].SetValue(_apchannel.Last.Value, isHot, ShowColdValues);

        // Upper band
        LinesSeries[1].SetValue(_apchannel.UpperBand, isHot, ShowColdValues);

        // Lower band
        LinesSeries[2].SetValue(_apchannel.LowerBand, isHot, ShowColdValues);
    }
}