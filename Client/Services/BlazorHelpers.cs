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
        /// Get processing log as formatted text
        /// </summary>
        public static string GetFormattedLog(this ConversionResult result)
        {
            return string.Join(Environment.NewLine, result.ProcessingLog);
        }
    }
}
