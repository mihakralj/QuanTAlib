using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AdxrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adxr? _adxr;
    private readonly LineSeries? _adxrSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ADXR {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/adxr/Adxr.Quantower.cs";

    public AdxrIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADXR - Average Directional Movement Rating";
        Description = "Quantifies the change in momentum of the ADX";

        _adxrSeries = new(name: "ADXR", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_adxrSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _adxr = new Adxr(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _adxr!.Update(this.GetInputBar(args), args.IsNewBar());

        _adxrSeries!.SetValue(result.Value, _adxr.IsHot, ShowColdValues);
    }
}
