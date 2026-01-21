using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// Maenv: Moving Average Envelope - Quantower Indicator Adapter
/// A percentage-based envelope using a selectable moving average as the middle line.
/// Middle = MA(source, period) - SMA, EMA, or WMA
/// Upper = Middle + (Middle × percentage / 100)
/// Lower = Middle - (Middle × percentage / 100)
/// </summary>
public sealed class MaenvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Percentage", sortIndex: 20, minimum: 0.01, maximum: 100.0, increment: 0.1, decimalPlaces: 2)]
    public double Percentage { get; set; } = 1.0;

    [InputParameter("MA Type", sortIndex: 30)]
    public MaenvType MaType { get; set; } = MaenvType.EMA;

    [InputParameter("Price Type", sortIndex: 40)]
    public PriceType SourceType { get; set; } = PriceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Maenv? _indicator;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"Maenv({Period},{Percentage},{MaType})";

    public MaenvIndicator()
    {
        Name = "Maenv - Moving Average Envelope";
        Description = "Percentage-based envelope using selectable MA (SMA/EMA/WMA)";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new Maenv(Period, Percentage, MaType);

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
