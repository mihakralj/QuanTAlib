using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TtmTrendIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, 1, 100, 1, 0)]
    public int Period { get; set; } = 6;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    public override string ShortName => $"TTM_TREND({Period})";

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    private TtmTrend _indicator = null!;
    private readonly LineSeries _series;

    public TtmTrendIndicator()
    {
        Name = "TTM Trend";
        Description = "John Carter's TTM Trend - EMA-based trend indicator with color-coded direction.";
        _series = new LineSeries("TTM Trend", Color.Gray, 3, LineStyle.Solid);
        AddLineSeries(_series);
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new TtmTrend(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var bar = this.GetInputBar(args);
        var result = _indicator.Update(bar, isNew);
        _series.SetValue(result.Value, _indicator.IsHot, ShowColdValues);

        // Color based on trend direction
        if (_indicator.IsHot)
        {
            Color trendColor = _indicator.Trend switch
            {
                1 => Color.Green,
                -1 => Color.Red,
                _ => Color.Gray
            };
            _series.SetMarker(0, trendColor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
    }
}
