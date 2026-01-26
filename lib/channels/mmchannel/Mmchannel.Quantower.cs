using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// Mmchannel: Min-Max Channel - Quantower Indicator Adapter
/// Upper = rolling highest high; Lower = rolling lowest low.
/// Uses streaming O(1) deques with bar-correction support.
/// </summary>
public sealed class MmchannelIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Mmchannel? _indicator;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"Mmchannel({Period})";

    public MmchannelIndicator()
    {
        Name = "Mmchannel - Min-Max Channel";
        Description = "Price channel using rolling highest high / lowest low without midpoint";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new Mmchannel(Period);

        AddLineSeries(new LineSeries("Upper", Color.FromArgb(255, 180, 180), 1, LineStyle.Solid));
        AddLineSeries(new LineSeries("Lower", Color.FromArgb(180, 180, 255), 1, LineStyle.Solid));
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

        LinesSeries[0].SetValue(_indicator.Upper.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(_indicator.Lower.Value, isHot, ShowColdValues);
    }
}
