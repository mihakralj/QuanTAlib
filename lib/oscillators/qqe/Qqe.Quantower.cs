using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class QqeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("RSI Period", sortIndex: 1, 1, 500, 1, 0)]
    public int RsiPeriod { get; set; } = 14;

    [InputParameter("Smooth Factor", sortIndex: 2, 1, 100, 1, 0)]
    public int SmoothFactor { get; set; } = 5;

    [InputParameter("QQE Factor", sortIndex: 3, 0.001, 50.0, 0.001, 3)]
    public double QqeFactor { get; set; } = 4.236;

    [IndicatorExtensions.DataSourceInput(sortIndex: 4)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Qqe _qqe = null!;
    private readonly LineSeries _qqeSeries;
    private readonly LineSeries _signalSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"QQE ({RsiPeriod},{SmoothFactor},{QqeFactor:G}):{Source}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/qqe/Qqe.cs";

    public QqeIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "QQE - Quantitative Qualitative Estimation";
        Description = "Multi-stage smoothed RSI oscillator with dynamic volatility-based trailing bands";

        _qqeSeries    = new LineSeries("QQE",    Color.Yellow, 2, LineStyle.Solid);
        _signalSeries = new LineSeries("Signal", Color.Cyan,   1, LineStyle.Solid);

        AddLineSeries(_qqeSeries);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _qqe = new Qqe(RsiPeriod, SmoothFactor, QqeFactor);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        _ = _qqe.Update(input, args.IsNewBar());

        if (!_qqe.IsHot && !ShowColdValues)
        {
            return;
        }

        _qqeSeries.SetValue(_qqe.QqeValue);
        _signalSeries.SetValue(_qqe.Signal);
    }
}
