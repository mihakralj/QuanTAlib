using TradingPlatform.BusinessLayer;
using System.Drawing;

namespace QuanTAlib
{
    public class CtiIndicator : Indicator
    {
        [InputParameter("Period", 0, 1, 100, 1, 0)]
        public int Period = 20;

        [InputParameter("Source Type", 1, variants: new object[]
        {
            "Open", SourceType.Open,
            "High", SourceType.High,
            "Low", SourceType.Low,
            "Close", SourceType.Close,
            "HL2", SourceType.HL2,
            "OC2", SourceType.OC2,
            "OHL3", SourceType.OHL3,
            "HLC3", SourceType.HLC3,
            "OHLC4", SourceType.OHLC4,
            "HLCC4", SourceType.HLCC4
        })]
        public SourceType SourceType = SourceType.Close;

        [InputParameter("Show Cold Values", 2)]
        public bool ShowColdValues = false;

        private Cti cti;
        protected LineSeries? CtiSeries;
        public int MinHistoryDepths => Period + 1;
        int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

        public CtiIndicator()
        {
            this.Name = "CTI - Ehler's Correlation Trend Indicator";
            this.Description = "A momentum oscillator that measures the correlation between the price and a lagged version of the price.";
            CtiSeries = new($"CTI {Period}", Color: IndicatorExtensions.Oscillators, 2, LineStyle.Solid);
            AddLineSeries(CtiSeries);
        }

        protected override void OnInit()
        {
            cti = new Cti(this.Period);
            base.OnInit();
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            TValue input = this.GetInputValue(args, Source);
            cti.Calc(value);

            CtiSeries!.SetValue(cti.Value);
            CtiSeries!.SetMarker(0, Color.Transparent);
        }

    public override string ShortName => $"CTI ({Period}:{SourceName})";

#pragma warning disable CA1416 // Validate platform compatibility
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, CtiSeries!, cti!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
    }
}
