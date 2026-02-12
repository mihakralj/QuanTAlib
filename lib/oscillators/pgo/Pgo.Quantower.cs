using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PgoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pgo _pgo = null!;
    private readonly LineSeries _series;
    private readonly LineSeries _zeroLine;
    private readonly LineSeries _obLine;
    private readonly LineSeries _osLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PGO ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/pgo/Pgo.Quantower.cs";

    public PgoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PGO - Pretty Good Oscillator";
        Description = "Distance from SMA normalized by ATR (units: ATR multiples)";

        _series = new LineSeries("PGO", Color.Yellow, 2, LineStyle.Solid);
        _zeroLine = new LineSeries("Zero", Color.Gray, 1, LineStyle.Solid);
        _obLine = new LineSeries("OB", Color.FromArgb(128, Color.Red), 1, LineStyle.Dash);
        _osLine = new LineSeries("OS", Color.FromArgb(128, Color.Green), 1, LineStyle.Dash);
        AddLineSeries(_series);
        AddLineSeries(_zeroLine);
        AddLineSeries(_obLine);
        AddLineSeries(_osLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _pgo = new Pgo(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[0, SeekOriginHistory.End];
        double open = item[PriceType.Open];
        double high = item[PriceType.High];
        double low = item[PriceType.Low];
        double close = item[PriceType.Close];
        double volume = item[PriceType.Volume];

        TBar bar = new(item.TimeLeft, open, high, low, close, volume);
        TValue result = _pgo.Update(bar, args.IsNewBar());

        if (!_pgo.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
        _zeroLine.SetValue(0.0);
        _obLine.SetValue(3.0);
        _osLine.SetValue(-3.0);
    }
}
