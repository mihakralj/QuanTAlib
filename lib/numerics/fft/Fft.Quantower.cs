using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// FFT (Fast Fourier Transform Dominant Cycle Detector) Quantower indicator.
/// Estimates the dominant cycle period in bars using Hanning-windowed DFT.
/// Output is the detected period in bars — displays in a separate window.
/// </summary>
public class FftIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Window Size", sortIndex: 0, minimum: 32, maximum: 128)]
    public int WindowSize { get; set; } = 64;

    [InputParameter("Min Period", sortIndex: 1, minimum: 2, maximum: 32)]
    public int MinPeriod { get; set; } = 4;

    [InputParameter("Max Period", sortIndex: 2, minimum: 4, maximum: 64)]
    public int MaxPeriod { get; set; } = 32;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Fft? _fft;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => WindowSize;
    public override string ShortName => $"FFT({WindowSize},{MinPeriod},{MaxPeriod})";

    public FftIndicator()
    {
        Name = "FFT - Fast Fourier Transform Dominant Cycle";
        Description = "Estimates dominant cycle period in bars using Hanning-windowed DFT";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        int clampedMax = Math.Min(MaxPeriod, WindowSize / 2);
        _fft = new Fft(WindowSize, MinPeriod, clampedMax);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Dominant Period", Color.Yellow, 2, LineStyle.Solid));
        AddLineSeries(new LineSeries("Max Period", Color.Gray, 1, LineStyle.Dash));
        AddLineSeries(new LineSeries("Min Period", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_fft == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _fft.Update(input, isNew);

        bool isHot = _fft.IsHot;
        int clampedMax = Math.Min(MaxPeriod, WindowSize / 2);

        LinesSeries[0].SetValue(_fft.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(clampedMax, isHot, ShowColdValues);
        LinesSeries[2].SetValue(MinPeriod, isHot, ShowColdValues);
    }
}
