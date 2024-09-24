using Xunit;
using QuanTAlib;

public class Consistency
{
    Random rnd;
    int series_len = 1000;
    int corrections = 100;

    public Consistency()
    { //constructor
        rnd = new((int)DateTime.Now.Ticks);
    }


    [Fact]
    public void CanUpdate()
    {

        GbmFeed gbm = new();
        TSeries input = new(gbm.Close);
        TSeries output = new(input);

        gbm.Add(10000);

        Assert.Equal(input.Count, output.Count);
        for (int i = 0; i < input.Count; i++)
        {
            Assert.Equal(input[i].v, output[i].v);
        }
    }

    [Fact]
    public void Alma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        double offset = rnd.Next();
        double sigma = rnd.Next(1, 100);
        Alma ma1 = new(period: p, offset: offset, sigma: sigma);
        Alma ma2 = new(period: p, offset: offset, sigma: sigma);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Convolution_isNew()
    {
        Convolution ma1 = new(new double[] { 1.0, 2, 3, 2, 1 });
        Convolution ma2 = new(new double[] { 1.0, 2, 3, 2, 1 });
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Dema_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Dema ma1 = new(p);
        Dema ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Dsma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Dsma ma1 = new(p);
        Dsma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Dwma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Dwma ma1 = new(p);
        Dwma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void EmaSma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Ema ma1 = new(p, useSma: true);
        Ema ma2 = new(p, useSma: true);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Ema_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Ema ma1 = new(p, useSma: false);
        Ema ma2 = new(p, useSma: false);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Sma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Sma ma1 = new(p);
        Sma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Epma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Epma ma1 = new(p);
        Epma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Frama_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Frama ma1 = new(p);
        Frama ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Fwma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Fwma ma1 = new(p);
        Fwma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Gma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Gma ma1 = new(p);
        Gma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Hma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Hma ma1 = new(p);
        Hma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }


    [Fact]
    public void Hwma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Hwma ma1 = new(p);
        Hwma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    /*
        [Fact]
        public void Jma_isNew()
        {
            int p = (int)rnd.Next(2, 100);
            Jma ma1 = new(p);
            Jma ma2 = new(p);
            for (int i = 0; i < series_len; i++)
            {
                TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
                ma1.Calc(item1);
                for (int j = 0; j < corrections; j++)
                {
                    item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                    ma1.Calc(item1);
                }
                ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));

                //Assert.Equal(ma1.Value, ma2.Value);
                Assert.True(ma1.Value == ma2.Value, $"Assertion failed at p={p}, Value={item1.Value}. ma1.Value={ma1.Value}, ma2.Value={ma2.Value}");
            }
        }
    */


    [Fact]
    public void Kama_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Kama ma1 = new(p);
        Kama ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Ltma_isNew()
    {
        int p = rnd.Next(0, 1);
        Ltma ma1 = new(p);
        Ltma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Mama_isNew()
    {
        int p = rnd.Next(0, 1);
        Mama ma1 = new(p, p * 0.1);
        Mama ma2 = new(p, p * 0.1);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);

            Assert.True(ma1.Value == ma2.Value, $"Assertion failed for p={p}, i={i}. Expected {ma1.Value} but got {ma2.Value}.");
        }
    }

    [Fact]
    public void Mgdi_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Mgdi ma1 = new(p);
        Mgdi ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Mma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Mma ma1 = new(p);
        Mma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Qema_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Qema ma1 = new();
        Qema ma2 = new();
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Rema_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Rema ma1 = new(p);
        Rema ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Rma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Rma ma1 = new(p);
        Rma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Sinema_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Sinema ma1 = new(p);
        Sinema ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Smma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Smma ma1 = new(p);
        Smma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void T3_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        T3 ma1 = new(p);
        T3 ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Tema_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Tema ma1 = new(p);
        Tema ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }


    [Fact]
    public void Trima_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Trima ma1 = new(p);
        Trima ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }


    [Fact]
    public void Vidya_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Vidya ma1 = new(p);
        Vidya ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Wma_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Wma ma1 = new(p);
        Wma ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }
    [Fact]
    public void Zlema_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Zlema ma1 = new(p);
        Zlema ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Entropy_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Entropy ma1 = new(p);
        Entropy ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Kurtosis_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Kurtosis ma1 = new(p);
        Kurtosis ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Max_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Max ma1 = new(p, 0.01);
        Max ma2 = new(p, 0.01);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Min_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Min ma1 = new(p, 0.01);
        Min ma2 = new(p, 0.01);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Med_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Median ma1 = new(p);
        Median ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Mode_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Mode ma1 = new(p);
        Mode ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Percentile_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Percentile ma1 = new(p, 50);
        Percentile ma2 = new(p, 50);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Skew_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Skew ma1 = new(p);
        Skew ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Stddev_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Stddev ma1 = new(p);
        Stddev ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Variance_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Variance ma1 = new(p);
        Variance ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

    [Fact]
    public void Zscore_isNew()
    {
        int p = (int)rnd.Next(2, 100);
        Zscore ma1 = new(p);
        Zscore ma2 = new(p);
        for (int i = 0; i < series_len; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
            ma1.Calc(item1);
            for (int j = 0; j < corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                ma1.Calc(item1);
            }
            ma2.Calc(new TValue(item1.Time, item1.Value, IsNew: true));
            Assert.Equal(ma1.Value, ma2.Value);
        }
    }

}
