using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// CHANGE (Percentage Change) Quantower indicator.
/// Calculates relative price movement over a lookback period.
/// Formula: (current - past) / past
/// </summary>
public class ChangeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period { get; set; } = 1;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Change? _change;
    private Func<IHistoryItem, double>? _selector;

    // Cached markers to avoid per-update allocations
    private static readonly IndicatorLineMarker GreenMarker = new(Color.Green);
    private static readonly IndicatorLineMarker RedMarker = new(Color.Red);
    private static readonly IndicatorLineMarker GrayMarker = new(Color.Gray);

    public int MinHistoryDepths => Period + 1;
    public override string ShortName => $"CHANGE({Period})";

    public ChangeIndicator()
    {
        Name = "CHANGE - Percentage Change";
        Description = "Calculates relative price movement: (current - past) / past";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _change = new Change(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Change", Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_change == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _change.Update(input, isNew);

        bool isHot = _change.IsHot;
        double changeValue = _change.Last.Value;  // Cache to avoid repeated property access

        LinesSeries[0].SetValue(changeValue, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            // Use cached markers to avoid per-update allocations
            IndicatorLineMarker marker = GetMarker(changeValue);
            LinesSeries[0].SetMarker(0, marker);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static IndicatorLineMarker GetMarker(double value)
    {
        if (!double.IsFinite(value)) return GrayMarker;
        if (value > 0) return GreenMarker;
        if (value < 0) return RedMarker;
        return GrayMarker;
    }
}
