using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ImpulseIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("EMA Period", sortIndex: 0, 1, 100, 1, 0)]
    public int EmaPeriod { get; set; } = 13;

    [InputParameter("MACD Fast", sortIndex: 1, 1, 100, 1, 0)]
    public int MacdFast { get; set; } = 12;

    [InputParameter("MACD Slow", sortIndex: 2, 1, 200, 1, 0)]
    public int MacdSlow { get; set; } = 26;

    [InputParameter("MACD Signal", sortIndex: 3, 1, 100, 1, 0)]
    public int MacdSignal { get; set; } = 9;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    public override string ShortName => $"IMPULSE({EmaPeriod},{MacdFast},{MacdSlow},{MacdSignal})";

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    private Impulse _indicator = null!;
    private readonly LineSeries _series;

    public ImpulseIndicator()
    {
        Name = "Elder Impulse System";
        Description = "Alexander Elder's Impulse System - combines 13-period EMA with MACD histogram for trend/momentum alignment.";
        _series = new LineSeries("EMA", Color.Gray, 2, LineStyle.Solid);
        AddLineSeries(_series);
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new Impulse(EmaPeriod, MacdFast, MacdSlow, MacdSignal);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var bar = this.GetInputBar(args);
        var result = _indicator.Update(bar, isNew);
        _series.SetValue(result.Value, _indicator.IsHot, ShowColdValues);

        if (_indicator.IsHot)
        {
            Color impulseColor = _indicator.Signal switch
            {
                1 => Color.Green,
                -1 => Color.Red,
                _ => Color.DodgerBlue
            };
            _series.SetMarker(0, impulseColor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
    }
}
