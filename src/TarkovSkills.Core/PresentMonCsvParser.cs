using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.Globalization;

namespace TarkovSkills.Core;

internal sealed record FrameCapture(PerformanceMetrics Metrics, CaptureWindow Window);

public static class PresentMonCsvParser
{
    public static PerformanceMetrics Parse(string path) => ParseCapture(path).Metrics;

    internal static FrameCapture ParseCapture(string path)
    {
        using var parser = new TextFieldParser(path) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(DetectDelimiter(File.ReadLines(path).First()));
        var headers = parser.ReadFields() ?? throw new InvalidDataException("PresentMon CSV has no header.");
        var index = FindHeader(headers, "MsBetweenPresents", "FrameTime", "CPUFrameTime", "MsBetweenDisplayChange");
        if (index < 0) index = Array.FindIndex(headers, h => h.Contains("frametime", StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new InvalidDataException("PresentMon CSV has no supported frametime column.");

        var betweenPresents = headers[index].Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase);
        var forwardFrame = headers[index].Equals("FrameTime", StringComparison.OrdinalIgnoreCase) ||
            headers[index].Equals("CPUFrameTime", StringComparison.OrdinalIgnoreCase);
        // Present QPC and CPU-start QPC are different instants. Match the timestamp
        // to the actual FPS column instead of treating CPU/GPU frame time as load.
        var timestamp = betweenPresents ? FindHeader(headers, "TimeInQPC", "QPCTime") :
            forwardFrame ? FindHeader(headers, "CPUStartQPC") : -1;
        var intervals = new List<CaptureInterval>();
        var values = new List<double>();
        var aligned = timestamp >= 0;
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields();
            if (row is null || row.Length <= index || !Number(row[index], out var value) || value <= 0 || value >= 10000) continue;
            values.Add(value);
            if (timestamp < 0 || row.Length <= timestamp || !ulong.TryParse(row[timestamp], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var qpc) || qpc == 0)
            {
                aligned = false;
                continue;
            }
            var time = qpc / (double)Stopwatch.Frequency;
            intervals.Add(betweenPresents ? new(time - value / 1000, time) : new(time, time + value / 1000));
        }
        if (values.Count < 120) throw new InvalidDataException("PresentMon capture contains too few frame samples.");
        values.Sort();
        var total = values.Sum();
        var oneCount = Math.Max(1, (int)Math.Ceiling(values.Count * .01));
        var pointOneCount = Math.Max(1, (int)Math.Ceiling(values.Count * .001));
        var metrics = new PerformanceMetrics(values.Count, Math.Round(total / 1000, 3), Math.Round(values.Count / (total / 1000), 2),
            Math.Round(1000 / values.TakeLast(oneCount).Average(), 2), Math.Round(1000 / values.TakeLast(pointOneCount).Average(), 2),
            Math.Round(total / values.Count, 3), Percentile(values, .95), Percentile(values, .99));
        return new(metrics, aligned ? CaptureWindow.FromFrames(intervals) : CaptureWindow.Unknown);
    }

    private static bool Number(string text, out double value) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static int FindHeader(string[] headers, params string[] names)
    {
        foreach (var name in names)
        {
            var index = Array.FindIndex(headers, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) return index;
        }
        return -1;
    }
    private static string DetectDelimiter(string header) => new[] { ",", ";", "\t" }.OrderByDescending(x => header.Split(x).Length).First();
    private static double Percentile(List<double> sorted, double percentile) => Math.Round(sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Count) - 1, 0, sorted.Count - 1)], 3);
}
