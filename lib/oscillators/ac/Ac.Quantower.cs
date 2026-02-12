using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AcIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int FastPeriod { get; set; } = 5;

    [InputParameter("Slow Period", sortIndex: 2, 1, 1000, 1, 0)]
    public int SlowPeriod { get; set; } = 34;

    [InputParameter("AC Period", sortIndex: 3, 1, 1000, 1, 0)]
    public int AcPeriod { get; set; } = 5;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ac _ac = null!;
    private readonly LineSeries _upSeries;
    private readonly LineSeries _downSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AC {FastPeriod}:{SlowPeriod}:{AcPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/ac/Ac.Quantower.cs";

    public AcIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "AC - Acceleration Oscillator";
        Description = "Measures acceleration/deceleration of market driving force";

        _upSeries = new LineSeries(name: "AC Up", color: Color.Green, width: 2, style: LineStyle.Solid);
        _downSeries = new LineSeries(name: "AC Down", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_upSeries);
        AddLineSeries(_downSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ac = new Ac(FastPeriod, SlowPeriod, AcPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _ac.Update(this.GetInputBar(args), args.IsNewBar());

        if (!_ac.IsHot && !ShowColdValues)
        {
            return;
        }

        double prevAc = double.NaN;
        if (Count > 1)
        {
            prevAc = _upSeries.GetValue(1);
            if (double.IsNaN(prevAc))
            {
                prevAc = _downSeries.GetValue(1);
            }
        }

        if (double.IsNaN(prevAc) || result.Value > prevAc)
        {
            _upSeries.SetValue(result.Value);
            _downSeries.SetValue(double.NaN);
        }
        else
        {
            _downSeries.SetValue(result.Value);
            _upSeries.SetValue(double.NaN);
        }
    }
}
