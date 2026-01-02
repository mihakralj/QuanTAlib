using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// APZ (Adaptive Price Zone) Quantower indicator.
/// Creates volatility-based adaptive bands around price using double-smoothed EMAs.
/// </summary>
public class ApzIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 11, minimum: 0.1, maximum: 10.0, increment: 0.1, decimalPlaces: 2)]
    public double Multiplier { get; set; } = 2.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Apz? _apz;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"APZ({Period},{Multiplier:F1})";

    public ApzIndicator()
    {
        Name = "APZ - Adaptive Price Zone";
        Description = "Volatility-based adaptive price channel using double-smoothed EMAs";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _apz = new Apz(Period, Multiplier);

        // Middle line (double-smoothed EMA of price)
        AddLineSeries(new LineSeries("Middle", Volatility, 2, LineStyle.Solid));

        // Upper band
        AddLineSeries(new LineSeries("Upper", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));

        // Lower band
        AddLineSeries(new LineSeries("Lower", Color.FromArgb(255, 160, 160), 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_apz == null) return;

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

        _apz.Update(input, isNew);

        bool isHot = _apz.IsHot;

        // Middle line
        LinesSeries[0].SetValue(_apz.Last.Value, isHot, ShowColdValues);

        // Upper band
        LinesSeries[1].SetValue(_apz.Upper.Value, isHot, ShowColdValues);

        // Lower band
        LinesSeries[2].SetValue(_apz.Lower.Value, isHot, ShowColdValues);
    }
}
