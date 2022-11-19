namespace QuanTAlib;
using System;

/* <summary>
Random Bars generator - used for testing, validation and fun
    Returns 'bars' number of candles that follow common market movement.
    volatility defines how 'jumpy' is the series of
    startvalue defines beginning closing price that then guides the rest of series
    
</summary> */

public class RND_Feed : TBars
{
    public RND_Feed(int Bars, double Volatility = 0.05, double Startvalue = 100.0)
    {
        Random rnd = new();
        double c = Startvalue;
        for (int i = 0; i < Bars; i++)
        {
            double o = Math.Round(c + (c * (((Volatility * 0.1) * rnd.NextDouble()) - 0.005)), 2);
            double h = Math.Round(o + (c * Volatility * rnd.NextDouble()), 2);
            double l = Math.Round(o - (c * Volatility * rnd.NextDouble()), 2);
            c = Math.Round(l + ((h - l) * rnd.NextDouble()), 2);
            double v = Math.Round(1000 * rnd.NextDouble(), 2);
            this.Add(DateTime.Today.AddDays(i - Bars), o, h, l, c, v);
        }
    }
}