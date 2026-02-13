using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class StochrsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("RSI Length", sortIndex: 1, 1, 500, 1, 0)]
    public int RsiLength { get; set; } = 14;

    [InputParameter("Stochastic Length", sortIndex: 2, 1, 500, 1, 0)]
    public int StochLength { get; set; } = 14;

    [InputParameter("K Smooth", sortIndex: 3, 1, 50, 1, 0)]
    public int KSmooth { get; set; } = 3;

    [InputParameter("D Smooth", sortIndex: 4, 1, 50, 1, 0)]
    public int DSmooth { get; set; } = 3;

    [IndicatorExtensions.DataSourceInput(sortIndex: 5)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Stochrsi _stochrsi = null!;
    private readonly LineSeries _kSeries;
    private readonly LineSeries _dSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"StochRSI ({RsiLength},{StochLength},{KSmooth},{DSmooth})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/stochrsi/Stochrsi.cs";

    public StochrsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "STOCHRSI - Stochastic RSI Oscillator";
        Description = "Applies the Stochastic formula to RSI values, producing %K and %D lines for overbought/oversold detection";

        _kSeries = new LineSeries("K", Color.Green, 2, LineStyle.Solid);
        _dSeries = new LineSeries("D", Color.Red, 2, LineStyle.Solid);

        AddLineSeries(_kSeries);
        AddLineSeries(_dSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _stochrsi = new Stochrsi(RsiLength, StochLength, KSmooth, DSmooth);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        _ = _stochrsi.Update(input, args.IsNewBar());

        if (!_stochrsi.IsHot && !ShowColdValues)
        {
            return;
        }

        _kSeries.SetValue(_stochrsi.K);
        _dSeries.SetValue(_stochrsi.D);
    }
}
