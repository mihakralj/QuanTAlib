using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class LunarIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Lunar _lunar = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _newMoonLine;
    private readonly LineSeries _fullMoonLine;
    private readonly LineSeries _quarterLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "LUNAR";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/lunar/Lunar.Quantower.cs";

    public LunarIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "LUNAR - Lunar Phase";
        Description = "Calculates Moon's illumination phase using orbital mechanics (0=New Moon, 1=Full Moon)";

        _series = new LineSeries(name: "Lunar Phase", color: Color.Gold, width: 2, style: LineStyle.Solid);
        _newMoonLine = new LineSeries(name: "New Moon", color: Color.DarkGray, width: 1, style: LineStyle.Dash);
        _fullMoonLine = new LineSeries(name: "Full Moon", color: Color.LightGoldenrodYellow, width: 1, style: LineStyle.Dash);
        _quarterLine = new LineSeries(name: "Quarter", color: Color.Gray, width: 1, style: LineStyle.Dot);
        AddLineSeries(_series);
        AddLineSeries(_newMoonLine);
        AddLineSeries(_fullMoonLine);
        AddLineSeries(_quarterLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _lunar = new Lunar();
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

        // Lunar phase uses only the timestamp, not the price
        var input = new TValue(time, 0);
        TValue result = _lunar.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _lunar.IsHot, ShowColdValues);
        _newMoonLine.SetValue(0.0);
        _fullMoonLine.SetValue(1.0);
        _quarterLine.SetValue(0.5);
    }
}