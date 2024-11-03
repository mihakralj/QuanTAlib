using System;
using System.Collections.Generic;
using System.Threading;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Integration;
using System.Diagnostics.CodeAnalysis;

namespace SyntheticVendorNamespace;

public class SyntheticVendor : Vendor
{
    private readonly List<MessageExchange> exchanges;
    private readonly List<MessageAsset> assets;
    private readonly List<MessageSymbol> symbols;

    public SyntheticVendor()
    {
        exchanges = new List<MessageExchange>
            {

                //Spike,
                //Impulse,
                //Triangle,
                //Sawtooth
                //Sine
                //Chirp
                //White
                //Gauss
                //B
                //HF
                //Impulse+HF,
                //Sawtooth+HF
                //Sine+G
                //Chirp+G
                //Complex
                //Market

                new MessageExchange { Id = "PU", ExchangeName = "1 Pulse" },
                new MessageExchange { Id = "WA", ExchangeName = "2 Wave" },
                new MessageExchange { Id = "MD", ExchangeName = "3 Modulation" },
                new MessageExchange { Id = "NO", ExchangeName = "4 Noise" },
                new MessageExchange { Id = "BR", ExchangeName = "5 Brownian" },
                new MessageExchange { Id = "QT", ExchangeName = "6 QuanTAlib" }
            };

        assets = new List<MessageAsset>
            {
                new MessageAsset { Id = "USD", Name = "USD" },

            };

        symbols = new List<MessageSymbol>
            {
                CreateMessageSymbol(id: "W1",  name: "1 Digital spike", exchangeId: "QT", assetId: "USD", type: SymbolType.Crypto,
                    description: "Sudden sharp spike in the signal"),
                CreateMessageSymbol("W2",  "2 Dirac delta spike", "QT", "USD", SymbolType.Crypto),
                CreateMessageSymbol("W8",  "4 Sinc pulse", "QT", "USD",  SymbolType.Crypto),

                CreateMessageSymbol("W3",  "1 Square Wave", "QT", "USD", SymbolType.ETF),
                CreateMessageSymbol("W4",  "2 Sawtooth Wave", "QT", "USD", SymbolType.ETF),
                CreateMessageSymbol("W5",  "3 Inverse sawtooth Wave", "QT", "USD", SymbolType.ETF),
                CreateMessageSymbol("W6",  "4 Triangle Wave", "QT", "USD", SymbolType.ETF),
                CreateMessageSymbol("W7",  "5 Sine Wave", "QT", "USD", SymbolType.ETF),

                CreateMessageSymbol("W11", "1 Amplitude modulation", "QT", "USD",  SymbolType.Forex),
                CreateMessageSymbol("W10", "2 Frequency sweep", "QT", "USD",  SymbolType.Forex),
                CreateMessageSymbol("W12", "3 Frequency modulation", "QT", "USD",  SymbolType.Forex),

                CreateMessageSymbol("W13", "1 White noise", "QT", "USD",  SymbolType.Indexes),
                CreateMessageSymbol("W14", "2 Pink noise", "QT", "USD",  SymbolType.Indexes),
                CreateMessageSymbol("W15", "3 Brown noise", "QT", "USD",  SymbolType.Indexes),

                CreateMessageSymbol("W16", "1 Fractional Brownian motion", "QT", "USD",  SymbolType.Synthetic),
                CreateMessageSymbol("W17", "2 Geometric Brownian motion", "QT", "USD",  SymbolType.Synthetic)
            };

        /*
            Bond,
            CFD,
                Crypto,
            Debentures,
            Equities,
                ETF,
            FixedIncome,
                Forex,
            Forward,
            Futures,
                Indexes,
            Options,
            Spot,
                Synthetic,
            Swap,
            Warrants,

        */



    }

    public static VendorMetaData GetVendorMetaData()
    {
        return new VendorMetaData()
        {
            VendorName = "Synthetic Vendor",
            VendorDescription = "A synthetic vendor for testing and demonstration purposes",
            GetDefaultConnections = () =>
            {
                var defaultConnection = Vendor.CreateDefaultConnectionInfo(
                    "Synthetic Connection",
                    "Synthetic Vendor",
                    "", // Replace with actual path if you have a logo
                    allowCreateCustomConnections: true
                );
                return new List<ConnectionInfo> { defaultConnection };
            }
        };
    }

    private MessageSymbol CreateMessageSymbol(
        string id,
        string name,
        string exchangeId,
        string assetId,
        SymbolType type,
        string description)
    {
        var messageSymbol = new MessageSymbol(id)
        {
            Name = name,
            Description = description,
            SymbolType = type,
            ExchangeId = exchangeId,
            ProductAssetId = assetId,

            // Setting some default values
            QuotingCurrencyAssetID = "USD",
            HistoryType = HistoryType.Last,
            DeltaCalculationType = DeltaCalculationType.TickDirection,
            LotSize = 1,
            VariableTickList = new List<VariableTick>
                {
                    new VariableTick(0.01) // Default tick size
                }
        };

        return messageSymbol;
    }




    private MessageSymbol CreateMessageSymbol(string id, string name, string exchangeId, string assetId, SymbolType type)
    {
        return new MessageSymbol(id)
        {
            Name = name,
            ExchangeId = exchangeId,
            ProductAssetId = assetId,
            QuotingCurrencyAssetID = "USD",
            QuotingType = SymbolQuotingType.LotSize,
            LotSize = 1,
            NettingType = NettingType.OnePosition,
            VolumeType = SymbolVolumeType.Volume,
            AllowCalculateRealtimeTicks = true,
            AllowCalculateRealtimeTrades = false,
            AllowCalculateRealtimeVolume = true,
            AllowCalculateRealtimeChange = true,
            AllowAbbreviatePriceByTickSize = false,
            NotionalValueStep = 0.01,
            DeltaCalculationType = DeltaCalculationType.AggressorFlag, // Changed from None to AggressorFlag
            MinVolumeAnalysisTickSize = 0.01,
            MaturityDate = DateTime.MaxValue, // Set to max value for non-expiring symbols
            HistoryType = HistoryType.Last,
            MinLot = 0.01,
            LotStep = 0.01,
            MaxLot = 1000000,
            SymbolType = type
            /*
                SymbolType.Unknown,
                [EnumMember] Forex,
                [EnumMember] Equities,
                [EnumMember] CFD,
                [EnumMember] Indexes,
                [EnumMember] Futures,
                [EnumMember] Options,
                [EnumMember] ETF,
                [EnumMember] Crypto,
                [EnumMember] Synthetic,
                [EnumMember] Spot,
                [EnumMember] Forward,
                [EnumMember] FixedIncome,
                [EnumMember] Warrants,

                [EnumMember] Debentures,
                [EnumMember] Bond,
                [EnumMember] Swap,
            */
        };
    }

    public override ConnectionResult Connect(ConnectRequestParameters connectRequestParameters)
    {
        // Simulating connection process
        Thread.Sleep(100); // Simulate some connection delay

        return ConnectionResult.CreateSuccess("Successfully connected to Synthetic Vendor");
    }

    public override void Disconnect()
    {
        // Simulating disconnection process
        Thread.Sleep(500); // Simulate some disconnection delay

    }

    public override PingResult Ping()
    {
        return new PingResult()
        {
            State = PingEnum.Connected,
            PingTime = TimeSpan.FromMilliseconds(2),
            RoundTripTime = TimeSpan.FromMilliseconds(2)
        };
    }


    public override IList<MessageExchange> GetExchanges(CancellationToken token)
    {
        return exchanges;
    }

    public override IList<MessageAsset> GetAssets(CancellationToken token)
    {
        return assets;
    }

    public override IList<MessageSymbol> GetSymbols(CancellationToken token)
    {
        return symbols;
    }

    public override void SubscribeSymbol(SubscribeQuotesParameters parameters)
    {
        // Empty method for data subscription to be filled later
    }

    public override void UnSubscribeSymbol(SubscribeQuotesParameters parameters)
    {
        // Empty method for data unsubscription to be filled later
    }


    public override IList<IHistoryItem> LoadHistory(HistoryRequestParameters requestParameters)
    {
        var historyItems = new List<IHistoryItem>();
        var symbolId = requestParameters.SymbolId;

        if (string.IsNullOrEmpty(symbolId)) return historyItems;

        DateTime from = requestParameters.FromTime;
        DateTime to = requestParameters.ToTime;

        TimeSpan periodTimeSpan = requestParameters.Aggregation.GetPeriod.Duration;

        // Define the maximum number of items to generate per request
        const int MAX_ITEMS_PER_REQUEST = 10000;

        Func<DateTime, TimeSpan, HistoryItemBar> waveGenerator = GetWaveGenerator(symbolId);

        DateTime currentTime = from;
        while (currentTime < to)
        {
            DateTime intervalEnd = currentTime.AddTicks(periodTimeSpan.Ticks * MAX_ITEMS_PER_REQUEST);
            if (intervalEnd > to)
                intervalEnd = to;

            while (currentTime <= intervalEnd)
            {
                var historyItem = waveGenerator(currentTime, periodTimeSpan); //calling generator fuction
                historyItems.Add(historyItem);

                currentTime = currentTime.Add(periodTimeSpan);

                if (requestParameters.CancellationToken.IsCancellationRequested) return historyItems;
            }

            currentTime = intervalEnd;
        }

        return historyItems;
    }

    private Func<DateTime, TimeSpan, HistoryItemBar> GetWaveGenerator(string symbolId)
    {
        switch (symbolId)
        {
            case "W1": return GenerateSpike;
            case "W2": return GenerateDiracDelta;
            case "W3": return GenerateSquareWave;
            case "W4": return GenerateSawtoothWave;
            case "W5": return GenerateInverseSawtoothWave;
            case "W6": return GenerateTriangleWave;
            case "W7": return GenerateSineWave;
            case "W8": return GenerateSincWave;
            case "W9": return GenerateGaussianPulse;
            case "W10": return GenerateFrequencySweep;
            case "W11": return GenerateAMSignal;
            case "W12": return GenerateFMSignal;
            case "W13": return GenerateWhiteNoise;
            case "W14": return GeneratePinkNoise;
            case "W15": return GenerateBrownNoise;
            case "W16": return GenerateFBM;
            case "W17": return GenerateGBM;

            default: return GenerateSineWave;
        }
    }

/*
    public override HistoryMetadata GetHistoryMetadata(CancellationToken cancellationToken)
    {
        return new HistoryMetadata
        {
            AllowedAggregations = new string[] { "Time", "Tick" },
            AllowedPeriodsHistoryAggregationTime = new Period[]
            {
            Period.SECOND1, Period.SECOND5, Period.SECOND10, Period.SECOND15, Period.SECOND30,
            Period.MIN1, Period.MIN2, Period.MIN3, Period.MIN4, Period.MIN5,
            Period.MIN10, Period.MIN15, Period.MIN30,
            Period.HOUR1, Period.HOUR2, Period.HOUR3, Period.HOUR4,
            Period.HOUR6, Period.HOUR8, Period.HOUR12,
            Period.DAY1,
            Period.WEEK1,
            Period.MONTH1,
            Period.YEAR1
            },
            AllowedBasePeriodsHistoryAggregationTime = new BasePeriod[]
            {
            BasePeriod.Second, BasePeriod.Minute, BasePeriod.Hour, BasePeriod.Day, BasePeriod.Week, BasePeriod.Month, BasePeriod.Year
            },
            AllowedHistoryTypesHistoryAggregationTime = new HistoryType[]
            {
            HistoryType.Bid,
            HistoryType.Ask,
            HistoryType.Midpoint,
            HistoryType.Last,
            HistoryType.BidAsk,
            HistoryType.Mark
            },
            AllowedHistoryTypesHistoryAggregationTick = new HistoryType[]
            {
            HistoryType.Bid,
            HistoryType.Ask,
            HistoryType.Midpoint,
            HistoryType.Last,
            HistoryType.BidAsk,
            HistoryType.Mark
            },
            DegreeOfParallelism = 1,
            UseHistoryLocalCache = false,
            BuildUncompletedBars = true
        };
    }
*/

    /*******************************************************************************************************************************************/
    /*******************************************************************************************************************************************/
    /*******************************************************************************************************************************************/
    /*******************************************************************************************************************************************/
    /*******************************************************************************************************************************************/
    /*******************************************************************************************************************************************/
    /*******************************************************************************************************************************************/

    private HistoryItemBar GenerateSpike(DateTime time, TimeSpan slice)
    {
        // Ensure we're working with UTC time
        DateTime utcTime = time.ToUniversalTime();

        // Calculate the number of hours since the epoch
        double hoursSinceEpoch = (utcTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalHours;

        // Calculate the position within the 25-hour cycle
        int cyclePosition = (int)Math.Floor(hoursSinceEpoch % 25);

        // Determine if this is a spike hour (hour 24 in the cycle) or the hour after
        bool isSpike = cyclePosition == 24;
        bool isAfterSpike = cyclePosition == 0;

        double openValue, closeValue;
        if (isSpike)
        {
            openValue = 0;
            closeValue = 100;
        }
        else if (isAfterSpike)
        {
            openValue = 100;
            closeValue = 0;
        }
        else
        {
            openValue = closeValue = 0.000001;
        }

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = openValue,
            High = Math.Max(openValue, closeValue),
            Low = Math.Min(openValue, closeValue),
            Close = closeValue,
            Volume = Math.Abs(closeValue - openValue),
            Ticks = time.Add(slice).Ticks - time.Ticks
        };
    }



    private HistoryItemBar GenerateDiracDelta(DateTime time, TimeSpan slice)
    {
        // Ensure we're working with UTC time
        DateTime utcTime = time.ToUniversalTime();

        // Calculate the start of the current day
        DateTime dayStart = utcTime.Date;

        // Determine which bar of the day we're on
        int barOfDay = (int)((utcTime - dayStart).Ticks / slice.Ticks);

        double openValue, closeValue;
        double scaleFactor = 100; // Scale factor to convert to percentage

        // Generate the spike pattern for the first 4 bars of each day
        switch (barOfDay)
        {
            case 0:
                openValue = 0.000001 * scaleFactor;
                closeValue = 0.05 * scaleFactor;
                break;
            case 1:
                openValue = 0.05 * scaleFactor;
                closeValue = 0.50 * scaleFactor;
                break;
            case 2:
                openValue = 0.50 * scaleFactor;
                closeValue = 0.05 * scaleFactor;
                break;
            case 3:
                openValue = 0.05 * scaleFactor;
                closeValue = 0.0000001 * scaleFactor;
                break;
            default:
                // Outside of the spike period, use baseline value
                openValue = closeValue = 0.000001;
                break;
        }

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = openValue,
            High = Math.Max(openValue, closeValue),
            Low = Math.Min(openValue, closeValue),
            Close = closeValue,
            Volume = Math.Abs(closeValue - openValue),
            Ticks = time.Add(slice).Ticks - time.Ticks
        };
    }



    private HistoryItemBar GenerateSineWave(DateTime time, TimeSpan slice)
    {
        // Ensure we're working with UTC time
        DateTime utcTime = time.ToUniversalTime();

        // Calculate the number of hours since the epoch
        double minutesSinceEpoch = (utcTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMinutes;

        // Calculate the position within the 25-hour cycle
        double cyclePosition = minutesSinceEpoch % 1500;


        // Calculate the sine wave values
        double frequency = 2 * Math.PI / 1500; // Complete cycle over 25 hours
        double value = 50 + 50 * Math.Sin(cyclePosition * frequency); // Oscillate between 0 and 100
        double nextValue = 50 + 50 * Math.Sin((cyclePosition + slice.TotalMinutes) * frequency);

        double factor = 0.6 * Math.Abs(nextValue - value);

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = value,

            High = Math.Max(value, nextValue) + factor,
            Low = Math.Min(value, nextValue) - factor,

            Close = nextValue,
            Volume = Math.Abs(nextValue - value) * 100, // Volume proportional to price change
            Ticks = time.Add(slice).Ticks - time.Ticks
        };
    }


    private HistoryItemBar GenerateSquareWave(DateTime time, TimeSpan slice)
    {
        // Ensure we're working with UTC time
        DateTime utcTime = time.ToUniversalTime();

        // Calculate the time within the day (in hours)
        double hoursInDay = utcTime.TimeOfDay.TotalHours;

        double openValue, closeValue;

        if (hoursInDay < 12)
        {
            // First half of the day
            openValue = 99;
            closeValue = 100;
        }
        else
        {
            // Second half of the day
            openValue = 1;
            closeValue = 0.0001;
        }

        // Handle transition bars
        if (Math.Abs(hoursInDay - 12) < slice.TotalHours / 2)
        {
            // Transition from 100 to 0 at noon
            openValue = 100;
            closeValue = 0.0001;
        }
        else if (hoursInDay < slice.TotalHours / 2 || hoursInDay > 24 - slice.TotalHours / 2)
        {
            // Transition from 0 to 100 at midnight
            openValue = 0.0001;
            closeValue = 100;
        }
        else
        {
            // No action
        }

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = openValue,
            High = Math.Max(openValue, closeValue),
            Low = Math.Min(openValue, closeValue),
            Close = closeValue,
            Volume = Math.Abs(closeValue - openValue),
            Ticks = time.Add(slice).Ticks - time.Ticks
        };
    }

    private HistoryItemBar GenerateSawtoothWave(DateTime time, TimeSpan slice)
    {
        double hours = (time - DateTime.UnixEpoch).TotalHours;
        double period = 24; // 24-hour period
        double position = hours % period;
        double value = 200 * (position / period) - 100;
        double nextValue = 200 * ((position + slice.TotalHours) % period / period) - 100;

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = value,
            High = Math.Max(value, nextValue),
            Low = Math.Min(value, nextValue),
            Close = nextValue,
            Volume = 100,
            Ticks = 100
        };
    }

    private HistoryItemBar GenerateInverseSawtoothWave(DateTime time, TimeSpan slice)
    {
        double hours = (time - DateTime.UnixEpoch).TotalHours;
        double period = 24; // 24-hour period
        double position = hours % period;
        double value = 100 - (200 * (position / period));
        double nextValue = 100 - (200 * ((position + slice.TotalHours) % period / period));

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = value,
            High = Math.Max(value, nextValue),
            Low = Math.Min(value, nextValue),
            Close = nextValue,
            Volume = 100,
            Ticks = 100
        };
    }


    private HistoryItemBar GenerateTriangleWave(DateTime time, TimeSpan slice)
    {
        double hours = (time - DateTime.UnixEpoch).TotalHours;
        double period = 24;
        double position = hours % period;
        double value = 200 * (Math.Abs(position / period - 0.5) - 0.25) * 100;
        double nextValue = 200 * (Math.Abs(((position + slice.TotalHours) % period) / period - 0.5) - 0.25) * 100;

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = value,
            High = Math.Max(value, nextValue),
            Low = Math.Min(value, nextValue),
            Close = nextValue,
            Volume = 100,
            Ticks = 100
        };
    }

    private HistoryItemBar GenerateSincWave(DateTime time, TimeSpan slice)
    {
        double minutes = (time - DateTime.UnixEpoch).TotalMinutes;
        double period = 1500.0; // 24-hour period
        double frequency = 2 * Math.PI / period; // Full cycle over 24 hours

        // Adjust time to center the main peak at 12 hours
        double t = minutes % period - period / 2;

        // Scale factor
        double scaleFactor = 7.0;

        // Calculate Sinc value
        double x = scaleFactor * frequency * t;
        double sincValue = x != 0 ? 100 * Math.Sin(x) / x : 100;

        // Calculate next value
        double nextT = ((minutes + slice.TotalMinutes) % period) - period / 2;
        double nextX = scaleFactor * frequency * nextT;
        double nextSincValue = nextX != 0 ? 100 * Math.Sin(nextX) / nextX : 100;

        // Ensure minimum value
        double minValue = 0.00001;
        sincValue = Math.Sign(sincValue) * Math.Max(Math.Abs(sincValue), minValue);
        nextSincValue = Math.Sign(nextSincValue) * Math.Max(Math.Abs(nextSincValue), minValue);

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = sincValue,
            High = Math.Max(sincValue, nextSincValue),
            Low = Math.Min(sincValue, nextSincValue),
            Close = nextSincValue,
            Volume = Math.Abs(nextSincValue - sincValue), // Volume as the change in value
            Ticks = slice.Ticks
        };
    }

    private HistoryItemBar GenerateGaussianPulse(DateTime time, TimeSpan slice)
    {
        double hours = (time - DateTime.UnixEpoch).TotalHours;
        double totalPeriod = 24.0; // 24-hour total cycle
        double pulsePeriod = 12.0; // 12-hour pulse duration
        double position = hours % totalPeriod;

        // Parameters for the Gaussian pulse
        double amplitude = 100.0; // Maximum amplitude
        double center = pulsePeriod / 2.0; // Center of the pulse (at 6 hours within the pulse period)
        double width = pulsePeriod / 6.0; // Width of the pulse (adjusts the spread)

        double baselineValue = 0.00001; // Value outside the pulse period

        // Calculate the Gaussian pulse value
        double value;
        if (position < pulsePeriod)
        {
            value = amplitude * Math.Exp(-Math.Pow(position - center, 2) / (2 * Math.Pow(width, 2))) + baselineValue;
        }
        else
        {
            value = baselineValue;
        }

        // Calculate the next value for the slice
        double nextPosition = (hours + slice.TotalHours) % totalPeriod;
        double nextValue;
        if (nextPosition < pulsePeriod)
        {
            nextValue = amplitude * Math.Exp(-Math.Pow(nextPosition - center, 2) / (2 * Math.Pow(width, 2))) + baselineValue;
        }
        else
        {
            nextValue = baselineValue;
        }

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = value,
            High = Math.Max(value, nextValue),
            Low = Math.Min(value, nextValue),
            Close = nextValue,
            Volume = Math.Abs(nextValue - value), // Volume as the change in value
            Ticks = slice.Ticks
        };
    }

    private HistoryItemBar GenerateFrequencySweep(DateTime time, TimeSpan slice)
    {
        double hours = (time - DateTime.UnixEpoch).TotalHours;
        double sweepPeriod = 48.0; // 48-hour period

        // Starting frequency (very low)
        double minFreq = Math.PI / 48.0;

        // Calculate the ending frequency to ensure continuity
        double maxFreq = Math.PI * 1.0 * Math.Exp(2 * Math.PI / sweepPeriod);

        // Calculate the exponential factor for frequency sweep
        double expFactor = Math.Log(maxFreq / minFreq) / sweepPeriod;

        // Calculate the overall phase up to the current time
        double totalPhase = (minFreq / expFactor) * (Math.Exp(expFactor * (hours % sweepPeriod)) - 1);

        // Shift the phase to start the cycle at 100 (cosine-like behavior)
        totalPhase += Math.PI / 2;

        // Calculate the value of the signal at the current time
        double value = 100.0 * Math.Sin(totalPhase);

        // Calculate the value of the signal at the end of the slice
        double nextPhase = (minFreq / expFactor) * (Math.Exp(expFactor * ((hours + slice.TotalHours) % sweepPeriod)) - 1);
        nextPhase += Math.PI / 2; // Apply the same phase shift
        double nextValue = 100.0 * Math.Sin(nextPhase);

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = value,
            High = Math.Max(value, nextValue),
            Low = Math.Min(value, nextValue),
            Close = nextValue,
            Volume = Math.Abs(nextValue - value), // Volume as the change in value
            Ticks = slice.Ticks
        };
    }

#pragma warning disable S2245
    // NOSONAR
    readonly Random random = new Random();
#pragma warning restore S2245
    private double currentAmplitude = 100;
    private HistoryItemBar GenerateAMSignal(DateTime time, TimeSpan slice)
    {
        double hours = (time - DateTime.UnixEpoch).TotalHours;
        double period = 12.0;
        double frequency = 2 * Math.PI / period; // Frequency for a 5-hour period

        // Determine the start of the current 5-hour cycle
        double cycleStartTime = Math.Floor(hours / period) * period;

        // Calculate the phase of the signal within the current 5-hour cycle
        double phase = frequency * (hours % period);

        // If we're at the start of a new 5-hour cycle, generate a new amplitude
        if (hours % period == 0)
        {
            currentAmplitude = random.NextDouble() * 100;
        }

        // Calculate the value of the signal at the current time
        double value = currentAmplitude * Math.Sin(phase);

        // Calculate the value of the signal at the end of the slice
        double nextPhase = frequency * ((hours + slice.TotalHours) % period);
        double nextValue = currentAmplitude * Math.Sin(nextPhase);

        // Create the HistoryItemBar
        var historyItem = new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = value,
            High = Math.Max(value, nextValue),
            Low = Math.Min(value, nextValue),
            Close = nextValue, // Set Close to the newly calculated value
            Volume = Math.Abs(nextValue), // Volume as the change in value
            Ticks = slice.Ticks
        };

        return historyItem;
    }


    private double currentFrequency = Math.PI / 220.0; // Initial frequency
    private double accumulatedPhase = 0;
    private double lastCloseValue = 0; // To store the last close value

    private HistoryItemBar GenerateFMSignal(DateTime time, TimeSpan slice)
    {
        double amplitude = 100.0; // Maximum amplitude
        double minFreq = Math.PI / 256.0;
        double maxFreq = Math.PI / 32.0;

        // Randomly adjust the frequency
        double frequencyStep = (maxFreq - minFreq) * 0.2; // 20% of the frequency range
        currentFrequency += (random.NextDouble() - 0.5) * 2 * frequencyStep;
        currentFrequency = Math.Max(minFreq, Math.Min(maxFreq, currentFrequency)); // Clamp frequency

        // Calculate phase increment for this slice
        double phaseIncrement = currentFrequency * slice.TotalHours;

        // Calculate the open value (which is the last close value)
        double openValue = lastCloseValue;

        // Calculate the close value
        accumulatedPhase += phaseIncrement;
        double closeValue = amplitude * Math.Sin(2 * Math.PI * accumulatedPhase);

        // Determine high and low values
        double midPhase = accumulatedPhase - (phaseIncrement / 2);
        double midValue = amplitude * Math.Sin(2 * Math.PI * midPhase);
        double highValue = Math.Max(Math.Max(openValue, closeValue), midValue);
        double lowValue = Math.Min(Math.Min(openValue, closeValue), midValue);

        // Store the close value for the next iteration
        lastCloseValue = closeValue;

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = openValue,
            High = highValue,
            Low = lowValue,
            Close = closeValue,
            Volume = Math.Abs(closeValue - openValue), // Volume as the change in value
            Ticks = slice.Ticks
        };
    }


    private HistoryItemBar GenerateWhiteNoise(DateTime time, TimeSpan slice)
    {
        double volatility = 2;
        double meanReversionStrength = 0.1;

        double openNoise = random.NextDouble();
        double open = previousClose + volatility * openNoise + meanReversionStrength * (meanPrice - previousClose);
        double closeNoise = random.NextDouble();
        double close = open + volatility * closeNoise + meanReversionStrength * (meanPrice - open);

        // Determine High and Low
        double high = Math.Max(open, close);
        double low = Math.Min(open, close);

        // Add variation to High and Low
        double highNoise = Math.Abs(random.NextDouble());
        high += volatility * highNoise;

        double lowNoise = Math.Abs(random.NextDouble());
        low -= volatility * lowNoise;

        double volume = Math.Abs(random.NextDouble()) * 1000 + 100;

        previousClose = close;


        // Create the HistoryItemBar
        var historyItem = new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Ticks = slice.Ticks
        };

        return historyItem;
    }



    private double previousClose = 50;
    private const double meanPrice = 50;

    private HistoryItemBar GeneratePinkNoise(DateTime time, TimeSpan slice)
    {
        double volatility = 2;
        double meanReversionStrength = 0.1;

        // Generate open price
        double openNoise = GeneratePinkNoiseValue();
        double open = previousClose + volatility * openNoise + meanReversionStrength * (meanPrice - previousClose);

        // Generate close price
        double closeNoise = GeneratePinkNoiseValue();
        double close = open + volatility * closeNoise + meanReversionStrength * (meanPrice - open);

        // Determine High and Low
        double high = Math.Max(open, close);
        double low = Math.Min(open, close);

        // Add variation to High and Low
        double highNoise = Math.Abs(GeneratePinkNoiseValue());
        high += volatility * highNoise;

        double lowNoise = Math.Abs(GeneratePinkNoiseValue());
        low -= volatility * lowNoise;

        double volume = Math.Abs(GeneratePinkNoiseValue()) * 1000 + 100;

        // Update previous close for the next iteration
        previousClose = close;

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Ticks = slice.Ticks
        };
    }


    private const int NumOctaves = 6;
    private readonly double[] pinkNoiseState = new double[NumOctaves];
    private double GeneratePinkNoiseValue()
    {
        double total = 0;

        for (int i = 0; i < NumOctaves; i++)
        {
            double white = random.NextDouble() * 2 - 1;
            pinkNoiseState[i] = (pinkNoiseState[i] + white) * 0.5;
            total += pinkNoiseState[i] * Math.Pow(2, -i);
        }

        // Normalize
        return total / NumOctaves;
    }



    private double lastValue = 0;

    private HistoryItemBar GenerateBrownNoise(DateTime time, TimeSpan slice)
    {
        double dt = slice.TotalDays / 365.0; // Time step in years
        double sigma = 25.0; // Annual volatility

        double increment = GenerateGaussian(0, sigma * Math.Sqrt(dt));
        double open = lastValue * (1 + GenerateGaussian(0, 0.05));
        double close = open + increment;

        // Simulate intra-period high and low
        double high = Math.Max(open, close);
        high += high * Math.Abs(GenerateGaussian(0, 0.06));
        double low = Math.Min(open, close);
        low -= low * Math.Abs(GenerateGaussian(0, 0.06));

        lastValue = close;

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = Math.Abs(close - open) * 1000, // Simplified volume calculation
            Ticks = slice.Ticks
        };
    }
    // Helper method to generate Gaussian distributed random numbers
    private double GenerateGaussian(double mean, double stdDev)
    {
        double u1 = 1.0 - random.NextDouble(); // Uniform(0,1] random doubles
        double u2 = 1.0 - random.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }



    private double GBMLastClose = 100; // Starting price
    private readonly double GBMMu = 0.05; // Annual drift
    private readonly double GBMSigma = 0.2; // Annual volatility

    private HistoryItemBar GenerateGBM(DateTime time, TimeSpan slice)
    {
        // Convert time slice to years
        double dt = slice.TotalDays / 365.0;

        // Generate a random normal variable for the main price movement
        double epsilon = GenerateGaussian(0, 1);

        // Calculate the price movement using GBM equation
        double drift = (GBMMu - 0.5 * GBMSigma * GBMSigma) * dt;
        double diffusion = GBMSigma * Math.Sqrt(dt) * epsilon;
        double returnValue = Math.Exp(drift + diffusion);

        // Add variability between previous close and current open
        double openVariability = GBMLastClose * GBMSigma * Math.Sqrt(dt) * GenerateGaussian(0, 1) * 0.1;
        double open = GBMLastClose + openVariability;

        // Calculate new close price
        double close = open * returnValue;

        // Generate High and Low values
        double highLowRange = Math.Max(Math.Abs(close - open), GBMLastClose * GBMSigma * Math.Sqrt(dt) * Math.Abs(GenerateGaussian(0, 1)));
        double high = Math.Max(open, close) + highLowRange * 0.5;
        double low = Math.Min(open, close) - highLowRange * 0.5;

        // Generate volume (you may want to adjust this based on your needs)
        double volume = Math.Max(100, 1000 * Math.Abs(close - open) + 500 * GenerateGaussian(0, 1));

        // Update last close for next iteration
        GBMLastClose = close;

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Ticks = slice.Ticks
        };
    }

    private double FBMLastClose = 100; // Starting price
    private readonly double FBMHurst = 0.85; // Hurst parameter (0.5 < H < 1 for persistent fBm)
    private readonly double FBMSigma = 0.25; // Volatility parameter
    private readonly double FBMDrift = 0.001; // drift

    private HistoryItemBar GenerateFBM(DateTime time, TimeSpan slice)
    {
        double dt = Math.Pow(slice.TotalDays / 365.0, 0.5);

        double epsilon = GenerateFractionalGaussianNoise(FBMHurst);

        double drift = FBMDrift * dt;
        double diffusion = FBMSigma * Math.Pow(dt, FBMHurst) * epsilon;

        double openVariability = FBMLastClose * FBMSigma * Math.Pow(dt, FBMHurst) * GenerateFractionalGaussianNoise(FBMHurst) * 0.1;
        double open = FBMLastClose + openVariability;

        double close = open * Math.Exp(drift + diffusion);

        double highLowRange = Math.Max(Math.Abs(close - open),
            FBMLastClose * FBMSigma * Math.Pow(dt, FBMHurst) * Math.Abs(GenerateFractionalGaussianNoise(FBMHurst)) * 2);
        double high = Math.Max(open, close) + highLowRange * 0.5;
        double low = Math.Min(open, close) - highLowRange * 0.5;

        double volume = Math.Max(100, 2000 * Math.Abs(close - open) +
            1000 * Math.Abs(GenerateFractionalGaussianNoise(FBMHurst)));

        FBMLastClose = close;

        return new HistoryItemBar
        {
            TicksLeft = time.Ticks,
            TicksRight = time.Add(slice).Ticks - 1,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Ticks = slice.Ticks
        };
    }

    private double GenerateFractionalGaussianNoise(double hurst)
    {
        double sum = 0;
        int n = 1000; // Number of terms in the approximation

        for (int i = 1; i <= n; i++)
        {
            double ri = GenerateGaussian(0, 1);
            sum += (Math.Pow(i, hurst - 0.5) - Math.Pow(i - 1, hurst - 0.5)) * ri;
        }

        return sum / Math.Sqrt(n);
    }



    // Add other necessary overrides and implementations as needed
}
