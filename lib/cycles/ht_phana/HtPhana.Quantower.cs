using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HtPhanaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Cycle Period", sortIndex: 1, minimum: 2, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 28;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private HtPhana _htPhana = null!;
    private readonly LineSeries _angleLine;
    private readonly LineSeries _derivedPeriodLine;
    private readonly LineSeries _trendStateLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HT_PHANA ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/ht_phana/HtPhana.Quantower.cs";

    public HtPhanaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HT_PHANA - Ehlers Phasor Analysis";
        Description = "Phasor analysis extracting cycle phase via Pearson correlation of price against cosine/sine reference waves, with wraparound compensation and trend state detection.";

        _angleLine = new LineSeries("Angle", Color.Yellow, 2, LineStyle.Solid);
        _derivedPeriodLine = new LineSeries("DerivedPeriod", Color.Cyan, 1, LineStyle.Solid);
        _trendStateLine = new LineSeries("TrendState", Color.Red, 2, LineStyle.Solid);
        AddLineSeries(_angleLine);
        AddLineSeries(_derivedPeriodLine);
        AddLineSeries(_trendStateLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _htPhana = new HtPhana(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _htPhana.Update(input, args.IsNewBar());

        _angleLine.SetValue(result.Value, _htPhana.IsHot, ShowColdValues);
        _derivedPeriodLine.SetValue(_htPhana.DerivedPeriod, _htPhana.IsHot, ShowColdValues);
        _trendStateLine.SetValue(_htPhana.TrendState, _htPhana.IsHot, ShowColdValues);
    }
}
