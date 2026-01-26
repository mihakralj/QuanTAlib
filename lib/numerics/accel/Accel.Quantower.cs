using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// ACCEL (Second Derivative / Acceleration) Quantower indicator.
/// Measures the rate of change of the rate of change - derivative of slope.
/// </summary>
public class AccelIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Accel? _accel;
    private Func<IHistoryItem, double>? _selector;

    // Cached markers to avoid per-update allocations
    private static readonly IndicatorLineMarker GreenMarker = new(Color.Green);
    private static readonly IndicatorLineMarker RedMarker = new(Color.Red);
    private static readonly IndicatorLineMarker GrayMarker = new(Color.Gray);

    public int MinHistoryDepths => 3;
    public override string ShortName => "ACCEL";

    public AccelIndicator()
    {
        Name = "ACCEL - Second Derivative (Acceleration)";
        Description = "Measures rate of change of rate of change - derivative of slope";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _accel = new Accel();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Accel", Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_accel == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        ProcessUpdateCore(item.TimeLeft, value, isNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessUpdateCore(DateTime time, double value, bool isNew)
    {
        // Validate non-finite inputs - use last valid if not finite
        if (!double.IsFinite(value))
        {
            value = _accel!.Last.Value;
            if (!double.IsFinite(value))
            {
                value = 0.0;
            }
        }

        TValue input = new(time, value);
        _accel!.Update(input, isNew);

        bool isHot = _accel.IsHot;
        double accelValue = _accel.Last.Value;  // Cache to avoid repeated property access

        LinesSeries[0].SetValue(accelValue, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            // Use cached markers to avoid per-update allocations
            IndicatorLineMarker marker = GetMarker(accelValue);
            LinesSeries[0].SetMarker(0, marker);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IndicatorLineMarker GetMarker(double value)
    {
        if (value > 0)
        {
            return GreenMarker;
        }

        if (value < 0)
        {
            return RedMarker;
        }

        return GrayMarker;
    }
}
