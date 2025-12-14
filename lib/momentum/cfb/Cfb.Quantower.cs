using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class CfbIndicator : Indicator, IWatchlistIndicator
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

    private Cfb? _cfb;
    private int _warmupBarIndex = -1;
    protected LineSeries? Series;
    protected string? SourceName;

    public int MinHistoryDepths => MaxLength;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CFB {MinLength}-{MaxLength}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/cfb/Cfb.Quantower.cs";

    public CfbIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "CFB - Jurik Composite Fractal Behavior";
        Description = "Trend Duration Index using fractal efficiency";
        Series = new(name: "CFB", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        // Generate lengths array
        int count = (MaxLength - MinLength) / Step + 1;
        int[] lengths = new int[count];
        for (int i = 0; i < count; i++)
        {
            lengths[i] = MinLength + i * Step;
        }

        _cfb = new Cfb(lengths);
        _warmupBarIndex = -1;
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        TValue result = _cfb!.Update(input, isNew);
        if (_warmupBarIndex < 0 && _cfb!.IsHot)
            _warmupBarIndex = Count;
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, _warmupBarIndex, showColdValues: ShowColdValues, tension: 0.2);
    }
}
