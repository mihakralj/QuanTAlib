using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MstochIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Stochastic Length", sortIndex: 1, 2, 500, 1, 0)]
    public int StochLength { get; set; } = 20;

    [InputParameter("HP Length", sortIndex: 2, 1, 500, 1, 0)]
    public int HpLength { get; set; } = 48;

    [InputParameter("SS Length", sortIndex: 3, 1, 500, 1, 0)]
    public int SsLength { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput(sortIndex: 4)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Mstoch _mstoch = null!;
    private readonly LineSeries _mstochSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MSTOCH ({StochLength},{HpLength},{SsLength})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/mstoch/Mstoch.cs";

    public MstochIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "MSTOCH - Ehlers MESA Stochastic";
        Description = "Ehlers MESA Stochastic: roofing filter + stochastic + super smoother, output [0,1]";

        _mstochSeries = new LineSeries(name: "MSTOCH", color: Color.Yellow, width: 2, style: LineStyle.Solid);

        AddLineSeries(_mstochSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _mstoch = new Mstoch(StochLength, HpLength, SsLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        _ = _mstoch.Update(new TValue(item.TimeLeft, price), args.IsNewBar());

        _mstochSeries.SetValue(_mstoch.Last.Value, _mstoch.IsHot, ShowColdValues);
    }
}
