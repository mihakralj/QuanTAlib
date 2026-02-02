using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for MASSI (Mass Index) indicator.
/// </summary>
[SkipLocalsInit]
public sealed class MassiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("EMA Length", sortIndex: 1, 1, 100, 1, 0)]
    public int EmaLength { get; set; } = 9;

    [InputParameter("Sum Length", sortIndex: 2, 1, 100, 1, 0)]
    public int SumLength { get; set; } = 25;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Massi _massi = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MASSI {EmaLength},{SumLength}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/massi/Massi.Quantower.cs";

    public MassiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "MASSI - Mass Index";
        Description = "Mass Index identifies trend reversals by measuring the narrowing and widening of the range between high and low prices. A 'reversal bulge' occurs when MASSI rises above 27 and then drops below 26.5.";

        _series = new LineSeries(name: "MASSI", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _massi = new Massi(EmaLength, SumLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        var bar = new TBar(
            item.TimeLeft.Ticks,
            item[PriceType.Open],
            item[PriceType.High],
            item[PriceType.Low],
            item[PriceType.Close],
            item[PriceType.Volume]
        );
        TValue result = _massi.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _massi.IsHot, ShowColdValues);
    }
}