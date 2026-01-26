using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// Sdchannel: Standard Deviation Channel - Quantower Indicator Adapter
/// Linear regression channel with standard deviation bands.
/// Middle = Linear regression line value at current bar
/// Upper = Middle + (StdDev × Multiplier)
/// Lower = Middle - (StdDev × Multiplier)
/// </summary>
public sealed class SdchannelIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 2, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 50;

    [InputParameter("Multiplier", sortIndex: 20, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 1)]
    public double Multiplier { get; set; } = 2.0;

    [InputParameter("Price Type", sortIndex: 30)]
    public PriceType SourceType { get; set; } = PriceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Sdchannel? _indicator;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"Sdchannel({Period},{Multiplier})";

    public SdchannelIndicator()
    {
        Name = "Sdchannel - Standard Deviation Channel";
        Description = "Linear regression channel with standard deviation bands";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new Sdchannel(Period, Multiplier);

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
            value: item[SourceType]
        );

        _indicator.Update(input, isNew);

        bool isHot = _indicator.IsHot;

        LinesSeries[0].SetValue(_indicator.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(_indicator.Upper.Value, isHot, ShowColdValues);
        LinesSeries[2].SetValue(_indicator.Lower.Value, isHot, ShowColdValues);
    }
}
