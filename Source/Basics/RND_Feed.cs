using System;
namespace QuanTAlib;

public class RND_Feed : TBars
{
    public RND_Feed(int days, double volatility = 0.05, double startvalue = 100.0)
    {
        Random rnd = new();
        double c = startvalue;
        for (int i = 0; i < days; i++)
        {
            double o = Math.Round(c + c * (volatility * 0.1 * rnd.NextDouble() - 0.005), 2);
            double h = Math.Round(o + c * volatility * rnd.NextDouble(), 2);
            double l = Math.Round(o - c * volatility * rnd.NextDouble(), 2);
            c = Math.Round(l + (h - l) * rnd.NextDouble(), 2);
            double v = Math.Round(1000 * rnd.NextDouble(), 2);
            this.Add(DateTime.Today.AddDays(i - days), o, h, l, c, v);
        }
    }
}