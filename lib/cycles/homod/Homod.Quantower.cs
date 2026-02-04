using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class HomodIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Min Period", sortIndex: 1, 3.0, 100.0, 0.5, 1)]
    public double MinPeriod { get; set; } = 6.0;

    [InputParameter("Max Period", sortIndex: 2, 4.0, 200.0, 0.5, 1)]
    public double MaxPeriod { get; set; } = 50.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Homod _homod = null!;
    private readonly LineSeries _cycleSeries;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HOMOD ({MinPeriod},{MaxPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/homod/Homod.Quantower.cs";

    public HomodIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "HOMOD - Homodyne Discriminator";
        Description = "Ehlers' Homodyne Discriminator estimates the dominant cycle period using homodyne multiplication and phase angle measurement";

        _cycleSeries = new LineSeries(name: "Cycle", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        AddLineSeries(_cycleSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _homod = new Homod(MinPeriod, MaxPeriod);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
        {
            return;
        }

        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        TValue result = _homod.Update(input, args.IsNewBar());

        _cycleSeries.SetValue(result.Value, _homod.IsHot, ShowColdValues);
    }
}