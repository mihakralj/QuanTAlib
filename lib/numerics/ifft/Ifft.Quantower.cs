using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// IFFT (Inverse FFT Spectral Low-Pass Filter) Quantower indicator.
/// Reconstructs a filtered price value by summing DC plus first N harmonics
/// of the Hanning-windowed DFT. Overlays on the price chart.
/// </summary>
public class IfftIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Window Size", sortIndex: 0, minimum: 32, maximum: 128)]
    public int WindowSize { get; set; } = 64;

    [InputParameter("Harmonics", sortIndex: 1, minimum: 1, maximum: 64)]
    public int NumHarmonics { get; set; } = 5;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Ifft? _ifft;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => WindowSize;
    public override string ShortName => $"IFFT({WindowSize},{NumHarmonics})";

    public IfftIndicator()
    {
        Name = "IFFT - Inverse FFT Spectral Low-Pass Filter";
        Description = "Spectral low-pass reconstruction using Hanning-windowed DFT harmonics";
        SeparateWindow = false;
    }

    protected override void OnInit()
    {
        _ifft = new Ifft(WindowSize, NumHarmonics);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("IFFT", Color.Cyan, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_ifft == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _ifft.Update(input, isNew);

        bool isHot = _ifft.IsHot;

        LinesSeries[0].SetValue(_ifft.Last.Value, isHot, ShowColdValues);
    }
}
