using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CfbIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Min Length", sortIndex: 1, 2, 1000, 1, 0)]
    public int MinLength { get; set; } = 2;

    [InputParameter("Max Length", sortIndex: 2, 2, 1000, 1, 0)]
    public int MaxLength { get; set; } = 192;

    [InputParameter("Step", sortIndex: 3, 1, 100, 1, 0)]
    public int Step { get; set; } = 2;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cfb _cfb = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CFB {MinLength}-{MaxLength}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/cfb/Cfb.Quantower.cs";

    public CfbIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "CFB - Jurik Composite Fractal Behavior";
        Description = "Trend Duration Index using fractal efficiency";
        _series = new LineSeries(name: "CFB", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        // Generate lengths array
        int count = ((MaxLength - MinLength) / Step) + 1;
        int[] lengths = new int[count];
        for (int i = 0; i < count; i++)
        {
            lengths[i] = MinLength + (i * Step);
        }

        _cfb = new Cfb(lengths);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _cfb.Update(new TValue(this.GetInputBar(args).Time, _priceSelector(HistoricalData[Count - 1, SeekOriginHistory.Begin])), args.IsNewBar());

        _series.SetValue(result.Value, _cfb.IsHot, ShowColdValues);
        _series.SetMarker(0, Color.Transparent);
    }
}
