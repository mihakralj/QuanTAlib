using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SolarIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Solar _solar = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _summerLine;
    private readonly LineSeries _winterLine;
    private readonly LineSeries _equinoxLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "SOLAR";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/solar/Solar.Quantower.cs";

    public SolarIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SOLAR - Solar Cycle";
        Description = "Calculates Sun's position in annual cycle (-1=Winter Solstice, 0=Equinox, +1=Summer Solstice)";

        _series = new LineSeries(name: "Solar Cycle", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _summerLine = new LineSeries(name: "Summer Solstice", color: Color.Red, width: 1, style: LineStyle.Dash);
        _winterLine = new LineSeries(name: "Winter Solstice", color: Color.Blue, width: 1, style: LineStyle.Dash);
        _equinoxLine = new LineSeries(name: "Equinox", color: Color.Gray, width: 1, style: LineStyle.Dot);
        AddLineSeries(_series);
        AddLineSeries(_summerLine);
        AddLineSeries(_winterLine);
        AddLineSeries(_equinoxLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _solar = new Solar();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
        {
            return;
        }

        var time = this.HistoricalData.Time();

        // Solar cycle uses only the timestamp, not the price
        var input = new TValue(time, 0);
        TValue result = _solar.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _solar.IsHot, ShowColdValues);
        _summerLine.SetValue(1.0);
        _winterLine.SetValue(-1.0);
        _equinoxLine.SetValue(0.0);
    }
}