using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PvdIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Price Period", sortIndex: 0, minimum: 1, maximum: 100, increment: 1, decimalPlaces: 0)]
    public int PricePeriod { get; set; } = 14;

    [InputParameter("Volume Period", sortIndex: 1, minimum: 1, maximum: 100, increment: 1, decimalPlaces: 0)]
    public int VolumePeriod { get; set; } = 14;

    [InputParameter("Smoothing Period", sortIndex: 2, minimum: 1, maximum: 50, increment: 1, decimalPlaces: 0)]
    public int SmoothingPeriod { get; set; } = 3;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pvd _pvd = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => Math.Max(PricePeriod, VolumePeriod) + SmoothingPeriod + 1;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => Math.Max(PricePeriod, VolumePeriod) + SmoothingPeriod + 1;

    public override string ShortName => "PVD";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/pvd/Pvd.Quantower.cs";

    public PvdIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PVD - Price Volume Divergence";
        Description = "Price Volume Divergence measures divergence between price and volume momentum";

        _series = new LineSeries(name: "PVD", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _pvd = new Pvd(PricePeriod, VolumePeriod, SmoothingPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _pvd.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _pvd.IsHot, ShowColdValues);
    }
}