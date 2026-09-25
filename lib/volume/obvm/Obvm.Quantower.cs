using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ObvmIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("OBVM Length", sortIndex: 1, 2, 500, 1, 0)]
    public int ObvmLength { get; set; } = 7;

    [InputParameter("Signal Length", sortIndex: 2, 2, 500, 1, 0)]
    public int SignalLength { get; set; } = 10;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Obvm _obvm = null!;
    private readonly LineSeries _obvmSeries;
    private readonly LineSeries _signalSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"OBVM {ObvmLength},{SignalLength}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/obvm/Obvm.cs";

    public ObvmIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "OBVM";
        Description = "On-Balance Volume Modified (Apirine)";

        _obvmSeries = new LineSeries(name: "OBVM", color: Color.DodgerBlue, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.OrangeRed, width: 1, style: LineStyle.Solid);

        AddLineSeries(_obvmSeries);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _obvm = new Obvm(ObvmLength, SignalLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _obvm.Update(this.GetInputBar(args), args.IsNewBar());

        _obvmSeries.SetValue(_obvm.ObvmValue.Value, _obvm.IsHot, ShowColdValues);
        _signalSeries.SetValue(_obvm.Signal.Value, _obvm.IsHot, ShowColdValues);
    }
}
