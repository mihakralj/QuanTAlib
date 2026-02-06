using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AlligatorIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Jaw Period", sortIndex: 1, 1, 100, 1, 0)]
    public int JawPeriod { get; set; } = 13;

    [InputParameter("Jaw Offset", sortIndex: 2, 0, 50, 1, 0)]
    public int JawOffset { get; set; } = 8;

    [InputParameter("Teeth Period", sortIndex: 3, 1, 100, 1, 0)]
    public int TeethPeriod { get; set; } = 8;

    [InputParameter("Teeth Offset", sortIndex: 4, 0, 50, 1, 0)]
    public int TeethOffset { get; set; } = 5;

    [InputParameter("Lips Period", sortIndex: 5, 1, 100, 1, 0)]
    public int LipsPeriod { get; set; } = 5;

    [InputParameter("Lips Offset", sortIndex: 6, 0, 50, 1, 0)]
    public int LipsOffset { get; set; } = 3;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Alligator _alligator = null!;
    private readonly LineSeries _jawSeries;
    private readonly LineSeries _teethSeries;
    private readonly LineSeries _lipsSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Alligator ({JawPeriod},{TeethPeriod},{LipsPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/alligator/Alligator.Quantower.cs";

    public AlligatorIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false; // Overlay on price chart
        Name = "Alligator";
        Description = "Williams Alligator - Three smoothed moving averages for trend identification";

        _jawSeries = new LineSeries(name: "Jaw", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _teethSeries = new LineSeries(name: "Teeth", color: Color.Red, width: 1, style: LineStyle.Solid);
        _lipsSeries = new LineSeries(name: "Lips", color: Color.Green, width: 1, style: LineStyle.Solid);

        AddLineSeries(_jawSeries);
        AddLineSeries(_teethSeries);
        AddLineSeries(_lipsSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _alligator = new Alligator(JawPeriod, JawOffset, TeethPeriod, TeethOffset, LipsPeriod, LipsOffset);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _alligator.Update(this.GetInputBar(args), args.IsNewBar());

        // Set values with offsets applied (Quantower handles the offset display)
        _jawSeries.SetValue(_alligator.Jaw.Value, _alligator.IsHot, ShowColdValues);
        _teethSeries.SetValue(_alligator.Teeth.Value, _alligator.IsHot, ShowColdValues);
        _lipsSeries.SetValue(_alligator.Lips.Value, _alligator.IsHot, ShowColdValues);
    }
}
