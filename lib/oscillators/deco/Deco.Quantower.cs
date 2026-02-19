using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DecoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Short Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int ShortPeriod { get; set; } = 30;

    [InputParameter("Long Period", sortIndex: 2, 2, 2000, 1, 0)]
    public int LongPeriod { get; set; } = 60;

    [IndicatorExtensions.DataSourceInput(sortIndex: 3)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Deco _deco = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DECO ({ShortPeriod},{LongPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/deco/Deco.Quantower.cs";

    public DecoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DECO - Ehlers Decycler Oscillator";
        Description = "Ehlers' Decycler Oscillator isolates intermediate cycles via dual HP filters";

        _series = new LineSeries("DECO", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _deco = new Deco(ShortPeriod, LongPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _deco.Update(input, args.IsNewBar());

        if (!_deco.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
