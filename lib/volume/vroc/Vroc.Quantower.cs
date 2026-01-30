using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VrocIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 1000, increment: 1)]
    public int Period { get; set; } = 12;

    [InputParameter("Use Percent", sortIndex: 20)]
    public bool UsePercent { get; set; } = true;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vroc _vroc = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => Period + 1;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => Period + 1;

    public override string ShortName => $"VROC({Period},{(UsePercent ? "%" : "pt")})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/vroc/Vroc.Quantower.cs";

    public VrocIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VROC - Volume Rate of Change";
        Description = "Measures the rate of change in volume over a specified period, either as a percentage or as absolute point change.";

        _series = new LineSeries(name: "VROC", color: Color.DodgerBlue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vroc = new Vroc(Period, UsePercent);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vroc.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _vroc.IsHot, ShowColdValues);
    }
}