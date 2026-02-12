using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DpoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dpo _dpo = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DPO ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/dpo/Dpo.Quantower.cs";

    public DpoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DPO - Detrended Price Oscillator";
        Description = "Removes trend from price by comparing current price to a displaced SMA";

        _series = new LineSeries("DPO", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dpo = new Dpo(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _dpo.Update(input, args.IsNewBar());

        if (!_dpo.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
