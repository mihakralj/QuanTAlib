using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HwcIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 2, 0.1, 10.0, 0.1, 1)]
    public double Multiplier { get; set; } = 1.0;

    [IndicatorExtensions.DataSourceInput(sortIndex: 3)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hwc _hwc = null!;
    private readonly LineSeries _upperSeries;
    private readonly LineSeries _middleSeries;
    private readonly LineSeries _lowerSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HWC({Period},{Multiplier:F1}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/channels/hwc/Hwc.Quantower.cs";

    public HwcIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        _sourceName = Source.ToString();
        Name = "HWC - Holt-Winter Channel";
        Description = "Adaptive volatility channel based on Holt-Winters triple exponential smoothing";

        _upperSeries = new LineSeries(name: "Upper", color: Color.Red, width: 1, style: LineStyle.Solid);
        _middleSeries = new LineSeries(name: "Middle", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _lowerSeries = new LineSeries(name: "Lower", color: Color.Green, width: 1, style: LineStyle.Solid);

        AddLineSeries(_upperSeries);
        AddLineSeries(_middleSeries);
        AddLineSeries(_lowerSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _hwc = new Hwc(Period, Multiplier);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = _priceSelector(item);
        TValue input = new(item.TimeLeft, price);

        _hwc.Update(input, args.IsNewBar());

        _upperSeries.SetValue(_hwc.Upper.Value, _hwc.IsHot, ShowColdValues);
        _middleSeries.SetValue(_hwc.Middle.Value, _hwc.IsHot, ShowColdValues);
        _lowerSeries.SetValue(_hwc.Lower.Value, _hwc.IsHot, ShowColdValues);
    }
}
