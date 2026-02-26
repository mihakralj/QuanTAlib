using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EthermIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 22;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Etherm _etherm = null!;
    private readonly LineSeries _tempSeries;
    private readonly LineSeries _signalSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ETHERM {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/etherm/Etherm.Quantower.cs";

    public EthermIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ETHERM - Elder's Thermometer";
        Description = "Measures bar-to-bar range extension to quantify market volatility";

        _tempSeries = new LineSeries(name: "Temperature", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Histogramm);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_tempSeries);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _etherm = new Etherm(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _etherm.Update(bar, args.IsNewBar());

        _tempSeries.SetValue(result.Value, _etherm.IsHot, ShowColdValues);
        _signalSeries.SetValue(_etherm.Signal, _etherm.IsHot, ShowColdValues);
    }
}
