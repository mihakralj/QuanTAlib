using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class GatorIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Jaw Period", sortIndex: 1, 1, 100, 1, 0)]
    public int JawPeriod { get; set; } = 13;

    [InputParameter("Jaw Shift", sortIndex: 2, 0, 50, 1, 0)]
    public int JawShift { get; set; } = 8;

    [InputParameter("Teeth Period", sortIndex: 3, 1, 100, 1, 0)]
    public int TeethPeriod { get; set; } = 8;

    [InputParameter("Teeth Shift", sortIndex: 4, 0, 50, 1, 0)]
    public int TeethShift { get; set; } = 5;

    [InputParameter("Lips Period", sortIndex: 5, 1, 100, 1, 0)]
    public int LipsPeriod { get; set; } = 5;

    [InputParameter("Lips Shift", sortIndex: 6, 0, 50, 1, 0)]
    public int LipsShift { get; set; } = 3;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Gator _gator = null!;
    private readonly LineSeries _upperSeries;
    private readonly LineSeries _lowerSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"GATOR {JawPeriod},{TeethPeriod},{LipsPeriod}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/gator/Gator.Quantower.cs";

    public GatorIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "GATOR - Williams Gator Oscillator";
        Description = "Dual-histogram oscillator from Williams Alligator. Upper = |Jaw−Teeth|, Lower = −|Teeth−Lips|";

        _upperSeries = new LineSeries(name: "Upper", color: Color.Lime, width: 2, style: LineStyle.Histogramm);
        _lowerSeries = new LineSeries(name: "Lower", color: Color.Red, width: 2, style: LineStyle.Histogramm);
        AddLineSeries(_upperSeries);
        AddLineSeries(_lowerSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _gator = new Gator(JawPeriod, JawShift, TeethPeriod, TeethShift, LipsPeriod, LipsShift);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double upper = _gator.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        double lower = _gator.Lower;
        _upperSeries.SetValue(upper, _gator.IsHot, ShowColdValues);
        _lowerSeries.SetValue(lower, _gator.IsHot, ShowColdValues);
    }
}
