using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeqConverter.Client.Services
{
    /// <summary>
    /// UI-friendly options model for Blazor components
    /// </summary>
    public class ConversionOptionsUI
    {
        public string EqualiserName { get; set; } = "StormAudio";

        public QValueType QType { get; set; } = QValueType.RBJ;

        public bool CombineShared { get; set; } = false;

        public bool IncludeParametricEQ { get; set; } = true;

        public bool IncludeAllPass { get; set; } = true;

        /// <summary>
        /// Whether to generate a channel settings file with delays, gains, and inversions
        /// </summary>
        public bool SaveChannelSettings { get; set; } = false;
        
    /// <summary>
    /// Unit to display delays in the UI
    /// </summary>
    public DelayUnit DelayDisplayUnit { get; set; } = DelayUnit.Milliseconds;

    /// <summary>
    /// Offset to add to all delay values (in the unit selected by DelayDisplayUnit)
    /// </summary>
    public double DelayOffset { get; set; } = 0.0;

        /// <summary>
        /// Convert UI options to internal conversion options
        /// </summary>
        public ConversionOptions ToConversionOptions()
        {
            var includedTypes = new List<string>();

            if (IncludeParametricEQ)
                includedTypes.Add("Parametric EQ");
            
            if (IncludeAllPass)
                includedTypes.Add("All-Pass");

            return new ConversionOptions
            {
                EqualiserName = EqualiserName,
                QType = QType == QValueType.RBJ ? "rbj" : "classic",
                CombineShared = CombineShared,
                IncludedTypes = includedTypes
            };
        }

        /// <summary>
        /// Create from internal conversion options
        /// </summary>
        public static ConversionOptionsUI FromConversionOptions(ConversionOptions options)
        {
            return new ConversionOptionsUI
            {
                EqualiserName = options.EqualiserName,
                QType = options.QType.Equals("classic", StringComparison.OrdinalIgnoreCase) 
                    ? QValueType.Classic 
                    : QValueType.RBJ,
                CombineShared = options.CombineShared,
                IncludeParametricEQ = options.IncludedTypes.Any(t => t.Contains("Parametric EQ", StringComparison.OrdinalIgnoreCase)),
                IncludeAllPass = options.IncludedTypes.Any(t => t.Contains("All-Pass", StringComparison.OrdinalIgnoreCase))
            };
        }
    }

    /// <summary>
    /// Q value type enumeration for UI
    /// </summary>
    public enum QValueType
    {
        RBJ,
        Classic
    }

    /// <summary>
    /// Units for delay display and offset in the UI
    /// </summary>
    public enum DelayUnit
    {
        Milliseconds,
        Meters,
        Feet
    }

    /// <summary>
    /// Service class for Blazor components to handle MSO conversion
    /// </summary>
    public class MsoConversionService
    {
        /// <summary>
        /// Convert MSO content with the given options
        /// </summary>
        public ConversionResult ConvertMso(string msoContent, ConversionOptionsUI options)
        {
            if (string.IsNullOrWhiteSpace(msoContent))
            {
                return new ConversionResult
                {
                    ProcessingLog = new List<string> { "Error: MSO content is empty or null" }
                };
            }

            try
            {
                var conversionOptions = options.ToConversionOptions();
                var converter = new MsoConverter(conversionOptions);
                return converter.Convert(msoContent);
            }
            catch (Exception ex)
            {
                return new ConversionResult
                {
                    ProcessingLog = new List<string> { $"Error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Get default conversion options
        /// </summary>
        public ConversionOptionsUI GetDefaultOptions()
        {
            return new ConversionOptionsUI();
        }

        /// <summary>
        /// Validate MSO content format
        /// </summary>
        public ValidationResult ValidateMsoContent(string msoContent)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(msoContent))
            {
                result.IsValid = false;
                result.ErrorMessage = "MSO content cannot be empty";
                return result;
            }

            // Check for basic MSO format markers
            var hasChannelMarkers = msoContent.Contains("Channel:") && msoContent.Contains("End Channel:");
            var hasFilterMarkers = System.Text.RegularExpressions.Regex.IsMatch(msoContent, @"FL\d+:");

            if (!hasChannelMarkers && !hasFilterMarkers)
            {
                result.IsValid = false;
                result.ErrorMessage = "Content does not appear to be in MSO format. Expected 'Channel:' and 'FL##:' markers.";
                return result;
            }

            result.IsValid = true;
            result.ErrorMessage = null;
            return result;
        }

        /// <summary>
        /// Get preview of what would be converted
        /// </summary>
        public ConversionPreview GetConversionPreview(string msoContent, ConversionOptionsUI options)
        {
            var preview = new ConversionPreview();

            try
            {
                var conversionOptions = options.ToConversionOptions();
                var converter = new MsoConverter(conversionOptions);
                var result = converter.Convert(msoContent);

                preview.ChannelCount = result.ChannelFiles.Count;
                preview.HasSharedFilters = !string.IsNullOrEmpty(result.SharedSubFile);
                preview.TotalFilters = result.TotalFiltersExported;
                preview.ProcessingMessages = result.ProcessingLog;
                preview.GainSettingsCount = result.GainSettings.Count;
                preview.DelaySettingsCount = result.DelaySettings.Count;
                preview.HasInversions = result.Inversions.HasInversions;
                preview.ChannelNames = result.ChannelFiles.Keys.Select(k => k.Replace("_filters.txt", "")).ToList();
            }
            catch (Exception ex)
            {
                preview.ProcessingMessages = new List<string> { $"Preview error: {ex.Message}" };
            }

            return preview;
        }

        /// <summary>
        /// Save options to JSON string for local storage
        /// </summary>
        public string SerializeOptions(ConversionOptionsUI options)
        {
            return JsonSerializer.Serialize(options, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        /// <summary>
        /// Load options from JSON string
        /// </summary>
        public ConversionOptionsUI? DeserializeOptions(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<ConversionOptionsUI>(json, new JsonSerializerOptions 
                { 
                    Converters = { new JsonStringEnumConverter() }
                });
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Validation result for MSO content
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Preview of conversion results
    /// </summary>
    public class ConversionPreview
    {
        public int ChannelCount { get; set; }
        public bool HasSharedFilters { get; set; }
        public int TotalFilters { get; set; }
        public List<string> ProcessingMessages { get; set; } = new List<string>();
        public int GainSettingsCount { get; set; }
        public int DelaySettingsCount { get; set; }
        public bool HasInversions { get; set; }
        
    /// <summary>
    /// Names of parsed channels (in same order as conversion result keys)
    /// </summary>
    public List<string> ChannelNames { get; set; } = new List<string>();
    }

    /// <summary>
    /// File download helper for Blazor
    /// </summary>
    public class FileDownloadInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MimeType { get; set; } = "text/plain";
    }

    /// <summary>
    /// Extension methods for Blazor integration
    /// </summary>
    public static class ConversionResultExtensions
    {
        /// <summary>
        /// Get all files ready for download
        /// </summary>
        public static List<FileDownloadInfo> GetDownloadFiles(this ConversionResult result)
        {
            var files = new List<FileDownloadInfo>();

            foreach (var (fileName, content) in result.ChannelFiles)
            {
                files.Add(new FileDownloadInfo
                {
                    FileName = fileName,
                    Content = content,
                    MimeType = "text/plain"
                });
            }

            if (!string.IsNullOrEmpty(result.SharedSubFile))
            {
                files.Add(new FileDownloadInfo
                {
                    FileName = "shared_sub_filters.txt",
                    Content = result.SharedSubFile,
                    MimeType = "text/plain"
                });
            }

            return files;
        }

        /// <summary>
        /// Get all files ready for download including channel settings if enabled
        /// </summary>
        public static List<FileDownloadInfo> GetDownloadFiles(this ConversionResult result, bool includeChannelSettings)
        {
            return GetDownloadFiles(result, includeChannelSettings, 0.0, DelayUnit.Milliseconds);
        }

        /// <summary>
        /// Get all files ready for download including channel settings if enabled with offset support
        /// </summary>
        public static List<FileDownloadInfo> GetDownloadFiles(this ConversionResult result, bool includeChannelSettings, double delayOffset, DelayUnit offsetUnit)
        {
            var files = GetDownloadFiles(result);

            if (includeChannelSettings && (result.DelaySettings.Any() || result.GainSettings.Any() || result.Inversions.HasInversions))
            {
                files.Add(new FileDownloadInfo
                {
                    FileName = "channel_settings.txt",
                    Content = GenerateChannelSettingsFile(result, delayOffset, offsetUnit),
                    MimeType = "text/plain"
                });
            }

            return files;
        }

        /// <summary>
        /// Generate channel settings file content with delays in all units, gains, and inversions
        /// </summary>
        public static string GenerateChannelSettingsFile(ConversionResult result)
        {
            return GenerateChannelSettingsFile(result, 0.0, DelayUnit.Milliseconds);
        }

        /// <summary>
        /// Generate channel settings file content with delays in all units, gains, and inversions
        /// </summary>
        public static string GenerateChannelSettingsFile(ConversionResult result, double delayOffset, DelayUnit offsetUnit)
        {
            var content = new System.Text.StringBuilder();
            const double speedOfSound_m_per_s = 343.0;

            content.AppendLine("MSO Channel Settings Export");
            content.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            content.AppendLine("=" + new string('=', 60));
            content.AppendLine();

            // DELAY SETTINGS SECTION
            if (result.DelaySettings.Any())
            {
                bool hasOffset = Math.Abs(delayOffset) > 0.001;

                // Convert offset to all units for display
                double offsetMs, offsetM, offsetFt;
                switch (offsetUnit)
                {
                    case DelayUnit.Milliseconds:
                        offsetMs = delayOffset;
                        offsetM = (delayOffset / 1000.0) * speedOfSound_m_per_s;
                        offsetFt = offsetM * 3.28084;
                        break;
                    case DelayUnit.Meters:
                        offsetM = delayOffset;
                        offsetMs = (delayOffset / speedOfSound_m_per_s) * 1000.0;
                        offsetFt = delayOffset * 3.28084;
                        break;
                    case DelayUnit.Feet:
                        offsetFt = delayOffset;
                        offsetM = delayOffset / 3.28084;
                        offsetMs = (offsetM / speedOfSound_m_per_s) * 1000.0;
                        break;
                    default:
                        offsetMs = offsetM = offsetFt = 0.0;
                        break;
                }

                content.AppendLine("DELAY SETTINGS");
                content.AppendLine("-" + new string('-', 40));
                content.AppendLine();

                if (hasOffset)
                {
                    content.AppendLine($"Offset applied: {offsetMs:F2} ms / {offsetM:F3} m / {offsetFt:F2} ft");
                    content.AppendLine();

                    // First table: Relative delays (without offset)
                    content.AppendLine("Relative Delays (MSO values without offset):");
                    content.AppendLine($"{"Channel",-10} {"Milliseconds",-15} {"Meters",-12} {"Feet",-12}");
                    content.AppendLine($"{new string('-', 10)} {new string('-', 15)} {new string('-', 12)} {new string('-', 12)}");

                    foreach (var delay in result.DelaySettings.OrderBy(d => d.ChannelName))
                    {
                        var delayMs = delay.DelayValue;
                        var delayMeters = (delayMs / 1000.0) * speedOfSound_m_per_s;
                        var delayFeet = delayMeters * 3.28084;

                        content.AppendLine($"{delay.ChannelName,-10} {delayMs,12:F2} ms {delayMeters,9:F3} m {delayFeet,9:F2} ft");
                    }
                    content.AppendLine();

                    // Second table: Absolute delays (with offset applied)
                    content.AppendLine("Absolute Delays (with offset applied):");
                    content.AppendLine($"{"Channel",-10} {"Milliseconds",-15} {"Meters",-12} {"Feet",-12}");
                    content.AppendLine($"{new string('-', 10)} {new string('-', 15)} {new string('-', 12)} {new string('-', 12)}");

                    foreach (var delay in result.DelaySettings.OrderBy(d => d.ChannelName))
                    {
                        var delayMs = delay.DelayValue;
                        var delayMeters = (delayMs / 1000.0) * speedOfSound_m_per_s;
                        var delayFeet = delayMeters * 3.28084;

                        // Apply offset
                        var absoluteMs = delayMs + offsetMs;
                        var absoluteM = delayMeters + offsetM;
                        var absoluteFt = delayFeet + offsetFt;

                        content.AppendLine($"{delay.ChannelName,-10} {absoluteMs,12:F2} ms {absoluteM,9:F3} m {absoluteFt,9:F2} ft");
                    }
                }
                else
                {
                    // Single table: No offset applied
                    content.AppendLine($"{"Channel",-10} {"Milliseconds",-15} {"Meters",-12} {"Feet",-12}");
                    content.AppendLine($"{new string('-', 10)} {new string('-', 15)} {new string('-', 12)} {new string('-', 12)}");

                    foreach (var delay in result.DelaySettings.OrderBy(d => d.ChannelName))
                    {
                        var delayMs = delay.DelayValue;
                        var delayMeters = (delayMs / 1000.0) * speedOfSound_m_per_s;
                        var delayFeet = delayMeters * 3.28084;

                        content.AppendLine($"{delay.ChannelName,-10} {delayMs,12:F2} ms {delayMeters,9:F3} m {delayFeet,9:F2} ft");
                    }
                }
                content.AppendLine();
            }

            // GAIN SETTINGS SECTION
            if (result.GainSettings.Any())
            {
                content.AppendLine("GAIN SETTINGS");
                content.AppendLine("-" + new string('-', 30));
                content.AppendLine();

                // Table header
                content.AppendLine($"{"Channel",-10} {"Gain (dB)",-12}");
                content.AppendLine($"{new string('-', 10)} {new string('-', 12)}");

                foreach (var gain in result.GainSettings.OrderBy(g => g.ChannelName))
                {
                    content.AppendLine($"{gain.ChannelName,-10} {gain.GainValue,9:F2} dB");
                }
                content.AppendLine();
            }

            // INVERSIONS SECTION
            content.AppendLine("CHANNEL INVERSIONS");
            content.AppendLine("-" + new string('-', 30));
            content.AppendLine();

            if (result.Inversions.HasInversions)
            {
                content.AppendLine($"{"Channel",-10} {"Status",-10}");
                content.AppendLine($"{new string('-', 10)} {new string('-', 10)}");

                foreach (var channel in result.Inversions.InvertedChannels.OrderBy(c => c))
                {
                    content.AppendLine($"{channel,-10} {"Inverted",-10}");
                }
            }
            else
            {
                content.AppendLine("No channel inversions detected.");
            }

            content.AppendLine();
            content.AppendLine("=" + new string('=', 60));
            content.AppendLine("Notes:");
            content.AppendLine("- Delay conversion assumes speed of sound = 343 m/s");
            content.AppendLine("- Distance delays represent acoustic path difference");
            if (Math.Abs(delayOffset) > 0.001)
            {
                content.AppendLine("- Relative delays show MSO values without offset");
                content.AppendLine("- Absolute delays include the specified offset");
            }
            content.AppendLine("- Apply these settings in your audio processor");

            return content.ToString();
        }

        /// <summary>
        /// Get processing log as formatted text
        /// </summary>
        public static string GetFormattedLog(this ConversionResult result)
        {
            return string.Join(Environment.NewLine, result.ProcessingLog);
        }
    }
}
