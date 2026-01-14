using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int FastPeriod { get; set; } = 5;

    [InputParameter("Slow Period", sortIndex: 2, 1, 1000, 1, 0)]
    public int SlowPeriod { get; set; } = 34;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ao _ao = null!;
    private readonly LineSeries _upSeries;
    private readonly LineSeries _downSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AO {FastPeriod}:{SlowPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/ao/Ao.Quantower.cs";

    public AoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "AO - Awesome Oscillator";
        Description = "Momentum indicator measuring market momentum";

        _upSeries = new LineSeries(name: "AO Up", color: Color.Green, width: 2, style: LineStyle.Solid);
        _downSeries = new LineSeries(name: "AO Down", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_upSeries);
        AddLineSeries(_downSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ao = new Ao(FastPeriod, SlowPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _ao.Update(this.GetInputBar(args), args.IsNewBar());

        if (!_ao.IsHot && !ShowColdValues)
            return;

        double prevAo = double.NaN;
        if (Count > 1)
        {
            prevAo = _upSeries.GetValue(1);
            if (double.IsNaN(prevAo))
            {
                prevAo = _downSeries.GetValue(1);
            }
        }

        if (double.IsNaN(prevAo) || result.Value > prevAo)
        {
            _upSeries.SetValue(result.Value);
            _downSeries.SetValue(double.NaN);
        }
        else
        {
            _upSeries.SetValue(double.NaN);
            _downSeries.SetValue(result.Value);
        }
    }
}
