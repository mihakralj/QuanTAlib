using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AdlIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adl? _adl;
    private readonly LineSeries? _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "ADL";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/adl/Adl.Quantower.cs";

    public AdlIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADL - Accumulation/Distribution Line";
        Description = "Accumulation/Distribution Line";

        _series = new LineSeries(name: "ADL", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _adl = new Adl();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _adl!.Update(bar, args.IsNewBar());

        _series!.SetValue(result.Value, _adl.IsHot, ShowColdValues);
    }
}
