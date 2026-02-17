// FRACTALS Validation Tests - Williams Fractals
// Cross-validated against Skender.Stock.Indicators GetFractal()
//
// Important alignment notes:
// - Skender reports fractal at the bar where the fractal occurs (bar[2] in our terms)
// - Our streaming indicator reports at the current bar (bar[0]) when detection happens
// - Therefore: our value at index i corresponds to Skender's value at index (i-2)
// - Skender naming: FractalBear = high point (resistance) = our UpFractal
//                   FractalBull = low point (support) = our DownFractal

using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public sealed class FractalsValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // -- Cross-library: Skender UpFractal (= Skender FractalBear) -----------------

    [Fact]
    public void StreamingMatchesSkender_UpFractal()
    {
        var _data = new ValidationTestData();

        // Skender: FractalBear = high point = our UpFractal
        var skenderResults = _data.SkenderQuotes
            .GetFractal()
            .ToList();

        // QuanTAlib streaming
        var f = new Fractals();
        var ourUpValues = new double[_data.Bars.Count];
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            _ = f.Update(_data.Bars[i], isNew: true);
            ourUpValues[i] = f.UpFractal;
        }

        // Compare with 2-bar offset: our value at i matches Skender at i-2
        int matched = 0;
        for (int i = 4; i < _data.Bars.Count; i++)
        {
            int skenderIdx = i - 2;
            if (skenderIdx < 0 || skenderIdx >= skenderResults.Count)
            {
                continue;
            }

            decimal? skenderBear = skenderResults[skenderIdx].FractalBear;
            bool skenderIsNull = !skenderBear.HasValue;
            bool ourIsNaN = double.IsNaN(ourUpValues[i]);

            if (skenderIsNull && ourIsNaN)
            {
                matched++;
                continue;
            }

            if (!skenderIsNull && !ourIsNaN)
            {
                Assert.Equal((double)skenderBear!.Value, ourUpValues[i], precision: 6);
                matched++;
            }
        }

        Assert.True(matched > 0, "Should have matched at least one warm value");
        _data.Dispose();
    }

    // -- Cross-library: Skender DownFractal (= Skender FractalBull) ---------------

    [Fact]
    public void StreamingMatchesSkender_DownFractal()
    {
        var _data = new ValidationTestData();

        // Skender: FractalBull = low point = our DownFractal
        var skenderResults = _data.SkenderQuotes
            .GetFractal()
            .ToList();

        // QuanTAlib streaming
        var f = new Fractals();
        var ourDownValues = new double[_data.Bars.Count];
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            _ = f.Update(_data.Bars[i], isNew: true);
            ourDownValues[i] = f.DownFractal;
        }

        // Compare with 2-bar offset: our value at i matches Skender at i-2
        int matched = 0;
        for (int i = 4; i < _data.Bars.Count; i++)
        {
            int skenderIdx = i - 2;
            if (skenderIdx < 0 || skenderIdx >= skenderResults.Count)
            {
                continue;
            }

            decimal? skenderBull = skenderResults[skenderIdx].FractalBull;
            bool skenderIsNull = !skenderBull.HasValue;
            bool ourIsNaN = double.IsNaN(ourDownValues[i]);

            if (skenderIsNull && ourIsNaN)
            {
                matched++;
                continue;
            }

            if (!skenderIsNull && !ourIsNaN)
            {
                Assert.Equal((double)skenderBull!.Value, ourDownValues[i], precision: 6);
                matched++;
            }
        }

        Assert.True(matched > 0, "Should have matched at least one warm value");
        _data.Dispose();
    }

    // -- Self-Consistency: Streaming == Batch --------------------------------------

    [Fact]
    public void StreamingMatchesBatch_UpFractal()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Fractals();
        var streamUp = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamUp[i] = streaming.UpFractal;
        }

        // Batch
        var batchResults = Fractals.Batch(bars);

        for (int i = 4; i < bars.Count; i++)
        {
            if (double.IsNaN(streamUp[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value), $"Mismatch at {i}: streaming=NaN, batch={batchResults[i].Value}");
            }
            else
            {
                Assert.Equal(streamUp[i], batchResults[i].Value, precision: 10);
            }
        }
    }

    // -- Self-Consistency: Streaming == Span ---------------------------------------

    [Fact]
    public void StreamingMatchesSpan_BothFractals()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Fractals();
        var streamUp = new double[bars.Count];
        var streamDown = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamUp[i] = streaming.UpFractal;
            streamDown[i] = streaming.DownFractal;
        }

        // Span
        var spanUp = new double[bars.Count];
        var spanDown = new double[bars.Count];
        Fractals.Batch(bars.HighValues, bars.LowValues, spanUp, spanDown);

        for (int i = 4; i < bars.Count; i++)
        {
            if (double.IsNaN(streamUp[i]))
            {
                Assert.True(double.IsNaN(spanUp[i]));
            }
            else
            {
                Assert.Equal(streamUp[i], spanUp[i], precision: 10);
            }

            if (double.IsNaN(streamDown[i]))
            {
                Assert.True(double.IsNaN(spanDown[i]));
            }
            else
            {
                Assert.Equal(streamDown[i], spanDown[i], precision: 10);
            }
        }
    }

    // -- Determinism ---------------------------------------------------------------

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var f1 = new Fractals();
        var f2 = new Fractals();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = f1.Update(bars[i], isNew: true);
            _ = f2.Update(bars[i], isNew: true);
        }

        Assert.Equal(f1.UpFractal, f2.UpFractal);
        Assert.Equal(f1.DownFractal, f2.DownFractal);
    }

    // -- Calculate Returns Valid Indicator -----------------------------------------

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = Fractals.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // -- BatchDual Returns Both Fractals ------------------------------------------

    [Fact]
    public void BatchDual_ReturnsBothSeries()
    {
        var bars = CreateGbmBars(count: 100);

        var (upSeries, downSeries) = Fractals.BatchDual(bars);

        Assert.Equal(bars.Count, upSeries.Count);
        Assert.Equal(bars.Count, downSeries.Count);

        // At least some fractals should be detected in 100 bars
        bool hasUp = false;
        bool hasDown = false;
        for (int i = 0; i < upSeries.Count; i++)
        {
            if (double.IsFinite(upSeries[i].Value))
            {
                hasUp = true;
            }
            if (double.IsFinite(downSeries[i].Value))
            {
                hasDown = true;
            }
        }

        Assert.True(hasUp, "Should detect at least one up fractal in 100 bars");
        Assert.True(hasDown, "Should detect at least one down fractal in 100 bars");
    }
}
