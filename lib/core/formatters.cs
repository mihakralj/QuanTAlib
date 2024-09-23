using Microsoft.DotNet.Interactive.Formatting;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QuanTAlib;
public static class Formatters
{
    const string smallfont = "smaller";
    const string pad = "18";
    public static void Initialize()
    {
        Formatter.Register<iTValue>((tick, writer) =>
        {
            var sb = new StringBuilder();
            sb.Append("<table style='border-collapse: collapse; text-align: left;'><tr>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0; font-size: {smallfont};'>{tick.Time:yyyy-MM-dd HH:mm:ss}</td>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{tick.Value:F2}</td>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{(tick.IsHot ? "🔥" : "❄️")}</td>");
            sb.Append("</tr></table>");
            writer.Write(sb.ToString());
        }, HtmlFormatter.MimeType);

        Formatter.Register<TSeries>((series, writer) =>
        {
            var sb = new StringBuilder();
            sb.Append("<table style='border-collapse: collapse; text-align: right;'></tr>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; text-align: left;'><b>{series.Name}</b></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>Index</i></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>Value</i></th>");
            sb.Append("</tr>");

            for (int i = 0; i < Math.Min(100, series.Count); i++)
            {
                TValue item = series[i];
                sb.Append("<tr>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0; font-size: {smallfont};'>{item.Time:yyyy-MM-dd HH:mm:ss}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0; font-size: {smallfont};'>{i}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{item.Value:F2}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{(item.IsHot ? "🔥" : "❄️")}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table>");
            if (series.Count > 100)
            {
                sb.Append("<p>Showing first 100 items. Total items: " + series.Count + "</p>");
            }
            writer.Write(sb.ToString());
        }, HtmlFormatter.MimeType);

        Formatter.Register<TBar>((bar, writer) =>
        {
            var sb = new StringBuilder();
            sb.Append("<table style='border-collapse: collapse; text-align: right;'><tr>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0; font-size: {smallfont};'>{bar.Time:yyyy-MM-dd HH:mm:ss}</td>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{bar.Open:F2}</td>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{bar.High:F2}</td>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{bar.Low:F2}</td>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{bar.Close:F2}</td>");
            sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'> {bar.Volume:F2}</td>");
            sb.Append("</tr></table>");
            writer.Write(sb.ToString());
        }, HtmlFormatter.MimeType);

        Formatter.Register<TBarSeries>((series, writer) =>
        {
            var sb = new StringBuilder();
            sb.Append("<table style='border-collapse: collapse; text-align: right;'></tr>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; text-align: left;'><b>{series.Name}</b></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>Index</i></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>Open</i></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>High</i></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>Low</i></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>Close</i></th>");
            sb.Append($"<th style='padding-left: {pad}px; padding-right: {pad}px; font-size: {smallfont};'><i>Volume</i></th>");
            sb.Append("</tr>");
            for (int i = 0; i < Math.Min(100, series.Count); i++)
            {
                TBar item = series[i];
                sb.Append("<tr>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0; font-size: {smallfont};'>{item.Time:yyyy-MM-dd HH:mm:ss}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0; font-size: {smallfont};'>{i}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{item.Open:F2}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{item.High:F2}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{item.Low:F2}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{item.Close:F2}</td>");
                sb.Append($"<td style='padding-left: {pad}px; padding-right: {pad}px; line-height: 1.0;'>{item.Volume:F2}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table>");
            if (series.Count > 100)
            {
                sb.Append("<p>Showing first 100 items. Total items: " + series.Count + "</p>");
            }
            writer.Write(sb.ToString());
        }, HtmlFormatter.MimeType);
    }
}
