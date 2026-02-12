using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BbbIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Multiplier", sortIndex: 2, 0.1, 10.0, 0.1, 1)]
    public double Multiplier { get; set; } = 2.0;

    [IndicatorExtensions.DataSourceInput(sortIndex: 3)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Bbb _bbb = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BBB ({Period},{Multiplier:F1})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/bbb/Bbb.Quantower.cs";

    public BbbIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "BBB - Bollinger %B";
        Description = "Position of price within Bollinger Bands";

        _series = new LineSeries("BBB", Color.Gold, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _bbb = new Bbb(Period, Multiplier);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _bbb.Update(input, args.IsNewBar());

        if (!_bbb.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
