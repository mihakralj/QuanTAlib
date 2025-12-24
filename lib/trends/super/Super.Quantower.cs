using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SuperIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Multiplier", sortIndex: 2, 0.1, 100.0, 0.1, 1)]
    public double Multiplier { get; set; } = 3.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Super? _super;
    private readonly LineSeries? _series;
    private readonly LineSeries? _upperBand;
    private readonly LineSeries? _lowerBand;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Super {Period}:{Multiplier}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/master/lib/trends/super/Super.Quantower.cs";

    public SuperIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "SuperTrend";
        Description = "SuperTrend Indicator";
        _series = new(name: "SuperTrend", color: Color.Orange, width: 2, style: LineStyle.Solid);
        _upperBand = new(name: "Upper Band", color: Color.Red, width: 1, style: LineStyle.Dot);
        _lowerBand = new(name: "Lower Band", color: Color.Green, width: 1, style: LineStyle.Dot);
        AddLineSeries(_series);
        AddLineSeries(_upperBand);
        AddLineSeries(_lowerBand);
    }

    protected override void OnInit()
    {
        _super = new Super(Period, Multiplier);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var bar = this.GetInputBar(args);
        double value = _super!.Update(bar, isNew).Value;
        
        _series!.SetValue(value, _super.IsHot, ShowColdValues);
        _upperBand!.SetValue(_super.UpperBand.Value, _super.IsHot, ShowColdValues);
        _lowerBand!.SetValue(_super.LowerBand.Value, _super.IsHot, ShowColdValues);

        // Color logic
        if (_super.IsHot)
        {
            _series!.SetMarker(0, _super.IsBullish ? Color.Green : Color.Red);
        }
    }
}
