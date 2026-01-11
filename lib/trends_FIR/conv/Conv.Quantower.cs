using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class ConvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Weights (comma separated)", sortIndex: 1)]
    public string WeightsInput { get; set; } = "0.1, 0.2, 0.3, 0.4";

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Conv? _conv;
    protected LineSeries? Series;
    protected string? SourceName;
    private Func<IHistoryItem, double>? _priceSelector;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CONV:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/conv/Conv.Quantower.cs";

    public ConvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "CONV - Convolution";
        Description = "Convolution with custom kernel";
        Series = new LineSeries(name: "CONV", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        try
        {
            var weightStrings = WeightsInput.Split(',');
            var weights = new double[weightStrings.Length];
            for (int i = 0; i < weightStrings.Length; i++)
            {
                weights[i] = double.Parse(weightStrings[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }

            _conv = new Conv(weights.Length == 0 ? [1.0] : weights);
        }
        catch (FormatException)
        {
            _conv = new Conv([1.0]);
        }
        catch (ArgumentException)
        {
            _conv = new Conv([1.0]);
        }

        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];

        TValue result = _conv!.Update(new TValue(item.TimeLeft.Ticks, _priceSelector!(item)), isNew: args.IsNewBar());

        Series!.SetValue(result.Value, _conv.IsHot, ShowColdValues);
    }
}
