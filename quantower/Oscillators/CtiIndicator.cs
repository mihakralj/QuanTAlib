using TradingPlatform.BusinessLayer;
using System.Drawing;

namespace QuanTAlib
{
    public class CtiIndicator : Indicator, IWatchlistIndicator
    {
        [InputParameter("Period", 0, 1, 100, 1, 0)]
        public int Period { get; set; } = 20;

        [IndicatorExtensions.DataSourceInput]
        public SourceType Source { get; set; } = SourceType.Close;

        [InputParameter("Show Cold Values", 2)]
        public bool ShowColdValues { get; set; } = true;

        private Cti? cti;
        protected LineSeries? Series;
        protected string? SourceName;
        public int MinHistoryDepths => Period + 1;
        int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

        public CtiIndicator()
        {
            OnBackGround = false;
            SeparateWindow = true;
            this.Name = "CTI - Ehler's Correlation Trend Indicator";
            SourceName = Source.ToString();
            this.Description = "A momentum oscillator that measures the correlation between the price and a lagged version of the price.";
            Series = new($"CTI {Period}", color: IndicatorExtensions.Oscillators, width: 2, LineStyle.Solid);
            AddLineSeries(Series);
        }

        protected override void OnInit()
        {
            cti = new Cti(this.Period);
            SourceName = Source.ToString();
            base.OnInit();
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            TValue input = this.GetInputValue(args, Source);
            TValue result = cti!.Calc(input);

            Series!.SetValue(result);
            Series!.SetMarker(0, Color.Transparent);
        }

        public override string ShortName => $"CTI ({Period}:{SourceName})";

#pragma warning disable CA1416 // Validate platform compatibility
        public override void OnPaintChart(PaintChartEventArgs args)
        {
            base.OnPaintChart(args);
            this.PaintSmoothCurve(args, Series!, cti!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.0);
        }
    }
}
