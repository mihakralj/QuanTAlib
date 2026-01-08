using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AdxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adx? _adx;
    private readonly LineSeries? _adxSeries;
    private readonly LineSeries? _diPlusSeries;
    private readonly LineSeries? _diMinusSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ADX {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/adx/Adx.Quantower.cs";

    public AdxIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADX - Average Directional Index";
        Description = "Measures the strength of a trend";

        _adxSeries = new(name: "ADX", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _diPlusSeries = new(name: "+DI", color: Color.Green, width: 1, style: LineStyle.Solid);
        _diMinusSeries = new(name: "-DI", color: Color.Red, width: 1, style: LineStyle.Solid);

        AddLineSeries(_adxSeries);
        AddLineSeries(_diPlusSeries);
        AddLineSeries(_diMinusSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _adx = new Adx(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _adx!.Update(this.GetInputBar(args), args.IsNewBar());

        _adxSeries!.SetValue(result.Value, _adx.IsHot, ShowColdValues);
        _diPlusSeries!.SetValue(_adx.DiPlus.Value, _adx.IsHot, ShowColdValues);
        _diMinusSeries!.SetValue(_adx.DiMinus.Value, _adx.IsHot, ShowColdValues);
    }
}
