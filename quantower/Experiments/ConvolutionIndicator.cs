using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ConvolutionIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Kernel (comma/space/semicolon separated numbers)", sortIndex: 1)]
    public string KernelString { get; set; } = "0.25, 0.5, 0.25, -0.5";

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Convolution? conv;
    private Mape? error;
    protected LineSeries? Series;
    protected string? SourceName;
    private double[]? kernel;
    public int MinHistoryDepths => kernel?.Length ?? 3;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public ConvolutionIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "CONV - Convolution Filter";
        Description = "Convolution Filter with custom kernel";
        kernel = ParseKernel(KernelString);
        Series = new(name: $"CONV {string.Join(",", kernel.Select(x => x.ToString("F2")))}",
                    color: IndicatorExtensions.Averages,
                    width: 2,
                    style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    private static double[] ParseKernel(string kernelStr)
    {
        // Split on common delimiters: comma, semicolon, space, tab, pipe
        var numbers = kernelStr.Split(new[] { ',', ';', ' ', '\t', '|' },
                                    StringSplitOptions.RemoveEmptyEntries |
                                    StringSplitOptions.TrimEntries);

        var kernel = new double[numbers.Length];
        for (int i = 0; i < numbers.Length; i++)
        {
            if (!double.TryParse(numbers[i], out kernel[i]))
            {
                // Default to simple 3-point moving average if parsing fails
                return new double[] { 0.25, 0.5, 0.25, -0.5 };
            }
        }
        return kernel;
    }

    protected override void OnInit()
    {
        kernel = ParseKernel(KernelString);
        conv = new Convolution(kernel);
        error = new(kernel.Length);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = conv!.Calc(input);
        error!.Calc(input, result);

        Series!.SetMarker(0, Color.Transparent);
        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"CONV {KernelString}:{SourceName}";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, kernel!.Length, showColdValues: ShowColdValues, tension: 0.2);
    }
}
