using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class InertiaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Inertia _inertia = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"INERTIA ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/inertia/Inertia.Quantower.cs";

    public InertiaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "INERTIA - Inertia Oscillator";
        Description = "Linear regression residual measuring price deviation from trend";

        _series = new LineSeries("INERTIA", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _inertia = new Inertia(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _inertia.Update(input, args.IsNewBar());

        if (!_inertia.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
