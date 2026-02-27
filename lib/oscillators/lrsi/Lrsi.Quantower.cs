using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class LrsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Gamma", sortIndex: 1, 0.0, 1.0, 0.01, 2)]
    public double Gamma { get; set; } = 0.5;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Lrsi _lrsi = null!;
    private readonly LineSeries _lrsiLine;

    public static int MinHistoryDepths => 4;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LRSI ({Gamma:F2})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/lrsi/Lrsi.Quantower.cs";

    public LrsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "LRSI - Laguerre RSI";
        Description = "Ehlers' Laguerre RSI: RSI computed over 4-stage Laguerre filter. Output [0,1]. Lower gamma = faster; higher = smoother.";

        _lrsiLine = new LineSeries("LRSI", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_lrsiLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _lrsi = new Lrsi(Gamma);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _lrsi.Update(input, args.IsNewBar());

        if (!_lrsi.IsHot && !ShowColdValues)
        {
            return;
        }

        _lrsiLine.SetValue(result.Value);
    }
}
