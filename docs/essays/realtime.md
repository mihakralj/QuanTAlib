# Historical vs. Real-time Indicators: A Tale of Two Approaches

**Indicators for historical analysis** are like long automation trains. They zoom through a complete set of provided historical data, crunching numbers faster in the series than you can say "bullish pattern." These indicators have the luxury of seeing the big picture all at once, from the oldest to the most current data point. That is allowing them to make end-to-end calculations with a bird's-eye view of market trends.

On the flip side, **real-time indicators** are more like surfers riding the wave of not-yet-known incoming data. They process information as it arrives, often dealing with updates and corrections to the most recent data point.

"*Currently the Close value of the bar is at \$3.10. Actually, it is at \$3.20. No, scrape that, it is at \$3.25, which also makes a new High of the current bar.*"

It's a bit like trying to predict the ocean's next move while you're already on the wave – exciting, but challenging! Unknown upcoming data trends alongside with the constant possiblity of corrections of the last provided value - that makes historical analysis indicators practically useless; they are fine-tuned to calculate an output on a well-known array of all known and valid historical inputs.


### The High-Frequency Data Dilemma

Imagine you attach your system to an active forex or crypto ticker, and you're receiving up to 200 updates per second to form a single one-second bar. 200 updates per second is not uncommon during an active trading rally of the day, sometimes exceeding 500 updates/second. That's a lot of data to process in real-time, right? Let's break it down:

- In one second: Up to 200 updates
- In one minute: 12,000 updates
- In one hour: 720,000 updates
- In 24 hours: 17,280,000 updates

Now, if we're talking about gathering 24 hours of one-second bars, we're looking at `86,400` data points (60 seconds * 60 minutes * 24 hours). And every single time we receive a new update (or a signal that a new bar started so the last bar is now sealed), we need to crunch through nearly 100,000 datapoints.  And do that 200 times per second. That's the calculation demand that will make even the most hard-core historical analysis indicator choke and give up.

### The Great Calculation Showdown

Let's compare how our historical and real-time approaches would handle this data tsunami:

**Historical Analysis Approach:**

- Imagine recalculating the entire history with each new or updated data point. It's like rewriting the entire encyclopedia every time you learn a new fact. With 17,280,000 updates in a day , you'd be needing:
- `17,280,000 * 86,400 = 1,492,992,000,000` calculations.
- That's nearly 1.5 trillion calculations! Your poor computer might just decide to pack its bags and go on vacation.

**Real-time Analysis Approach:**

- Our real-time indicator doesn't need to recalculate the entire history. It just processes each new (or updated) data point as it arrives, and spits out the result. So, we're looking at a mere 17,280,000 calculations per day, one single calculation per each update.

### Why This Matters

This enormous difference in calculation requirements isn't just about saving your computer from a meltdown. It's about providing traders with insights when they use tens of indicators with many parameter variations across hundreds of tracked symbols. Real-time indicators allow for quicker decision-making, more responsive trading strategies, and the ability to catch market movements as they happen.

So, the next time someone tells you that fine-tuned historical indicators are basically faster than performance of real-time indicators, you can wow them with your newfound knowledge. Just remember, in the world of technical analysis, being real-time isn't just a feature – it's a superpower!

**Real-time analysis is like having a super-efficient personal assistant who only tells you what's new, while historical analysis is like that friend who insists on retelling you their entire life story every time you meet up for coffee.**