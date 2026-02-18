using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AgcIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Decay", sortIndex: 1, 0.9, 0.9999, 0.001, 3)]
    public double Decay { get; set; } = 0.991;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Agc _agc = null!;
    private Roofing _roofing = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AGC {Decay:F3}:{_sourceName}";

    public AgcIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "AGC - Automatic Gain Control";
        Description = "Ehlers AGC: amplitude normalization via exponential peak tracking, applied after Roofing filter";
        _series = new LineSeries(name: $"AGC {Decay:F3}", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _roofing = new Roofing(48, 10);
        _agc = new Agc(Decay);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double price = _priceSelector(item);

        // First apply roofing filter to get oscillating signal, then normalize with AGC
        double filtered = _roofing.Update(new TValue(item.TimeLeft.Ticks, price), isNew).Value;
        double value = _agc.Update(new TValue(item.TimeLeft.Ticks, filtered), isNew).Value;
        _series.SetValue(value, _agc.IsHot, ShowColdValues);
    }
}
