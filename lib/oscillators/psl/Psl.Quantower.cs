using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PslIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 5000, 1, 0)]
    public int Period { get; set; } = 12;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Psl _psl = null!;
    private readonly LineSeries _pslLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PSL ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/psl/Psl.Quantower.cs";

    public PslIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PSL - Psychological Line";
        Description = "Percentage of up-bars over a lookback period";

        _pslLine = new LineSeries("PSL", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_pslLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _psl = new Psl(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _psl.Update(input, args.IsNewBar());

        if (!_psl.IsHot && !ShowColdValues)
        {
            return;
        }

        _pslLine.SetValue(result.Value);
    }
}
