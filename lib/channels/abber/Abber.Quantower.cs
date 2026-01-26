using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// ABBER (Aberration Bands) - Volatility bands using absolute deviation
/// A Quantower indicator adapter that provides three bands based on mean absolute deviation
/// rather than standard deviation, making it more robust to outliers than Bollinger Bands.
/// </summary>
[SkipLocalsInit]
public sealed class AbberIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 2, 0.1, 10.0, 0.1, 2)]
    public double Multiplier { get; set; } = 2.0;

    [InputParameter("Data source", sortIndex: 3, variants: [
        "Open", SourceType.Open,
        "High", SourceType.High,
        "Low", SourceType.Low,
        "Close", SourceType.Close,
        "HL/2 (Median)", SourceType.HL2,
        "OC/2 (Midpoint)", SourceType.OC2,
        "OHL/3 (Mean)", SourceType.OHL3,
        "HLC/3 (Typical)", SourceType.HLC3,
        "OHLC/4 (Average)", SourceType.OHLC4,
        "HLCC/4 (Weighted)", SourceType.HLCC4
    ])]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Abber? _abber;
    private Func<IHistoryItem, double>? _selector;
    private readonly LineSeries _middleSeries;
    private readonly LineSeries _upperSeries;
    private readonly LineSeries _lowerSeries;

    public int MinHistoryDepths => Period;

    public override string ShortName => $"ABBER {Period},{Multiplier:F1}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/abber/Abber.Quantower.cs";

    public AbberIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "ABBER - Aberration Bands";
        Description = "Volatility bands using absolute deviation (robust to outliers)";

        _middleSeries = new LineSeries(name: "Middle", color: Color.FromArgb(255, 128, 128), width: 2, style: LineStyle.Solid);
        _upperSeries = new LineSeries(name: "Upper", color: Color.FromArgb(255, 160, 160), width: 1, style: LineStyle.Dash);
        _lowerSeries = new LineSeries(name: "Lower", color: Color.FromArgb(255, 160, 160), width: 1, style: LineStyle.Dash);

        AddLineSeries(_middleSeries);
        AddLineSeries(_upperSeries);
        AddLineSeries(_lowerSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _abber = new Abber(Period, Multiplier);
        _selector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (HistoricalData.Count == 0 || _abber is null || _selector is null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        TValue input = new(item.TimeLeft, value);

        _abber.Update(input, args.IsNewBar());

        _middleSeries.SetValue(_abber.Last.Value, _abber.IsHot, ShowColdValues);
        _upperSeries.SetValue(_abber.Upper.Value, _abber.IsHot, ShowColdValues);
        _lowerSeries.SetValue(_abber.Lower.Value, _abber.IsHot, ShowColdValues);
    }
}
