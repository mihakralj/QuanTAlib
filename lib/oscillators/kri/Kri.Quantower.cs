using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class KriIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 5000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Kri _kri = null!;
    private readonly LineSeries _kriLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"KRI ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/kri/Kri.Quantower.cs";

    public KriIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "KRI - Kairi Relative Index";
        Description = "Percentage deviation of price from its SMA";

        _kriLine = new LineSeries("KRI", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_kriLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _kri = new Kri(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _kri.Update(input, args.IsNewBar());

        if (!_kri.IsHot && !ShowColdValues)
        {
            return;
        }

        _kriLine.SetValue(result.Value);
    }
}
