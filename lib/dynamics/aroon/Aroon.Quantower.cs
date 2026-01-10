using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AroonIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Aroon? _aroon;
    private readonly LineSeries? _upSeries;
    private readonly LineSeries? _downSeries;
    private readonly LineSeries? _oscSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Aroon {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/aroon/Aroon.Quantower.cs";

    public AroonIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Aroon";
        Description = "Identifies trend changes and strength";

        _upSeries = new LineSeries(name: "Aroon Up", color: Color.Green, width: 1, style: LineStyle.Solid);
        _downSeries = new LineSeries(name: "Aroon Down", color: Color.Red, width: 1, style: LineStyle.Solid);
        _oscSeries = new LineSeries(name: "Aroon Osc", color: Color.Blue, width: 2, style: LineStyle.Solid);

        AddLineSeries(_upSeries);
        AddLineSeries(_downSeries);
        AddLineSeries(_oscSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _aroon = new Aroon(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _aroon!.Update(this.GetInputBar(args), args.IsNewBar());

        _upSeries!.SetValue(_aroon.Up.Value, _aroon.IsHot, ShowColdValues);
        _downSeries!.SetValue(_aroon.Down.Value, _aroon.IsHot, ShowColdValues);
        _oscSeries!.SetValue(result.Value, _aroon.IsHot, ShowColdValues);
    }
}
