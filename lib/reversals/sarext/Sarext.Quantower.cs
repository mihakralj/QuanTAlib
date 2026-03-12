using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// SAREXT (Parabolic SAR Extended) Quantower indicator.
/// Extended Parabolic SAR with asymmetric acceleration factors for long/short positions.
/// Sign-encoded output: positive = long, negative = short.
/// </summary>
[SkipLocalsInit]
public sealed class SarextIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Start Value", sortIndex: 0, -1000.0, 1000.0, 0.01, 2)]
    public double StartValue { get; set; } = 0;

    [InputParameter("Offset On Reverse", sortIndex: 1, 0.0, 100.0, 0.01, 2)]
    public double OffsetOnReverse { get; set; } = 0;

    [InputParameter("AF Init Long", sortIndex: 2, 0.001, 1.0, 0.001, 3)]
    public double AfInitLong { get; set; } = 0.02;

    [InputParameter("AF Long", sortIndex: 3, 0.001, 1.0, 0.001, 3)]
    public double AfLong { get; set; } = 0.02;

    [InputParameter("AF Max Long", sortIndex: 4, 0.001, 1.0, 0.01, 2)]
    public double AfMaxLong { get; set; } = 0.20;

    [InputParameter("AF Init Short", sortIndex: 5, 0.001, 1.0, 0.001, 3)]
    public double AfInitShort { get; set; } = 0.02;

    [InputParameter("AF Short", sortIndex: 6, 0.001, 1.0, 0.001, 3)]
    public double AfShort { get; set; } = 0.02;

    [InputParameter("AF Max Short", sortIndex: 7, 0.001, 1.0, 0.01, 2)]
    public double AfMaxShort { get; set; } = 0.20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Sarext _indicator = null!;
    private readonly LineSeries _sarSeries;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName =>
        $"SAREXT({AfInitLong:F2},{AfMaxLong:F2},{AfInitShort:F2},{AfMaxShort:F2})";

    public override string SourceCodeLink =>
        "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/sarext/Sarext.cs";

    public SarextIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "SAREXT - Parabolic SAR Extended";
        Description = "Extended Parabolic SAR with asymmetric acceleration factors for long and short positions. Sign-encoded output.";

        _sarSeries = new LineSeries(name: "SAREXT", color: Color.DodgerBlue, width: 2, style: LineStyle.Dot);
        AddLineSeries(_sarSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Sarext(StartValue, OffsetOnReverse,
            AfInitLong, AfLong, AfMaxLong,
            AfInitShort, AfShort, AfMaxShort);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        double sarValue = _indicator.Last.Value;
        double displayValue = Math.Abs(sarValue);

        _sarSeries.SetValue(displayValue, _indicator.IsHot, ShowColdValues);

        if (_indicator.IsHot || ShowColdValues)
        {
            Color color = _indicator.IsLong ? Color.Green : Color.Red;
            _sarSeries.SetMarker(0, new IndicatorLineMarker(color));
        }
    }
}
