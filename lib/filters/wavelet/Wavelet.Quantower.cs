using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class WaveletIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Decomposition Levels", sortIndex: 1, 1, 8, 1, 0)]
    public int Levels { get; set; } = 4;

    [InputParameter("Threshold Multiplier", sortIndex: 2, 0.0, 5.0, 0.1, 1)]
    public double ThreshMult { get; set; } = 1.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Wavelet _wavelet = null!;
    private readonly LineSeries _waveletSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"WAVELET {Levels}:{ThreshMult:F1}:{_sourceName}";

    public WaveletIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "WAVELET - À Trous Wavelet Denoising Filter";
        Description = "Non-decimated wavelet transform with Haar basis and soft thresholding for signal denoising";
        _waveletSeries = new LineSeries(name: $"Wavelet {Levels}", color: Color.Purple, width: 2, style: LineStyle.Solid);
        AddLineSeries(_waveletSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _wavelet = new Wavelet(Levels, ThreshMult);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _wavelet.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _waveletSeries.SetValue(value, _wavelet.IsHot, ShowColdValues);
    }
}
