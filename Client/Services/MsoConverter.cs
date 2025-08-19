using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PeqConverter.Client.Services
{
    /// <summary>
    /// Configuration options for MSO to Storm Audio conversion
    /// </summary>
    public class ConversionOptions
    {
        /// <summary>
        /// Q value type to use: "rbj" or "classic"
        /// </summary>
        public string QType { get; set; } = "rbj";

        /// <summary>
        /// Filter types to include in conversion
        /// </summary>
        public List<string> IncludedTypes { get; set; } = new List<string> { "Parametric EQ", "All-Pass" };

        /// <summary>
        /// Whether to combine shared sub filters with individual channel filters
        /// </summary>
        public bool CombineShared { get; set; } = false;

        /// <summary>
        /// Equalizer name to use in output files
        /// </summary>
        public string EqualiserName { get; set; } = "StormAudio";
    }

    /// <summary>
    /// Represents a parsed filter with its parameters
    /// </summary>
    public class FilterData
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Frequency { get; set; }
        public double Gain { get; set; }
        public double Q { get; set; }
        public double QRbj { get; set; }
        public double QClassic { get; set; }
    }

    /// <summary>
    /// Represents gain settings for a channel
    /// </summary>
    public class ChannelGainSetting
    {
        public string ChannelName { get; set; } = string.Empty;
        public double GainValue { get; set; }
    }

    /// <summary>
    /// Represents delay settings for a channel
    /// </summary>
    public class ChannelDelaySetting
    {
        public string ChannelName { get; set; } = string.Empty;
        public double DelayValue { get; set; }
    }

    /// <summary>
    /// Represents inversion settings for channels
    /// </summary>
    public class ChannelInversions
    {
        public List<string> InvertedChannels { get; set; } = new List<string>();
        public bool HasInversions => InvertedChannels.Any();
    }

    /// <summary>
    /// Result of the conversion process
    /// </summary>
    public class ConversionResult
    {
        public Dictionary<string, string> ChannelFiles { get; set; } = new Dictionary<string, string>();
        public string? SharedSubFile { get; set; }
        public int TotalFiltersProcessed { get; set; }
        public int TotalFiltersExported { get; set; }
        public List<string> ProcessingLog { get; set; } = new List<string>();
        public List<ChannelGainSetting> GainSettings { get; set; } = new List<ChannelGainSetting>();
        public List<ChannelDelaySetting> DelaySettings { get; set; } = new List<ChannelDelaySetting>();
        public ChannelInversions Inversions { get; set; } = new ChannelInversions();
    }

    /// <summary>
    /// Main converter class for MSO to Storm Audio format conversion
    /// </summary>
    public class MsoConverter
    {
        private readonly ConversionOptions _options;

        public MsoConverter(ConversionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Convert MSO format to Storm Audio format
        /// </summary>
        /// <param name="msoContent">Content of the MSO file</param>
        /// <returns>Conversion result with generated files and statistics</returns>
        public ConversionResult Convert(string msoContent)
        {
            var result = new ConversionResult();
            
            try
            {
                // Parse the MSO file
                var (channels, sharedSub) = ParseMsoFile(msoContent);
                
                // Parse gain and delay settings
                ParseGainAndDelaySettings(msoContent, result);
                
                // Parse channel inversions
                ParseChannelInversions(msoContent, result);
                
                result.ProcessingLog.Add($"Using Q type: {_options.QType.ToUpper()}");
                
                if (_options.IncludedTypes.Any())
                {
                    result.ProcessingLog.Add($"Included filter types: {string.Join(", ", _options.IncludedTypes)}");
                }
                
                if (_options.CombineShared && sharedSub.Any())
                {
                    result.ProcessingLog.Add($"Combining {sharedSub.Count} shared sub filters with individual channel filters");
                }
                
                result.ProcessingLog.Add("=" + new string('=', 59));

                // Process individual channels
                foreach (var (channelName, filters) in channels)
                {
                    if (filters.Any())
                    {
                        List<FilterData> combinedFilters;
                        
                        if (_options.CombineShared && sharedSub.Any())
                        {
                            // Shared filters first, then channel's own filters
                            combinedFilters = sharedSub.Concat(filters).ToList();
                            var channelFile = WriteStormAudioFormat(combinedFilters, channelName, _options.EqualiserName);
                            result.ChannelFiles[$"{channelName}_filters.txt"] = channelFile;
                            
                            result.ProcessingLog.Add($"Channel {channelName}: {sharedSub.Count} shared + {filters.Count} channel = {combinedFilters.Count} total filters exported to {channelName}_filters.txt");
                            result.TotalFiltersProcessed += filters.Count; // Count channel filters only once
                            result.TotalFiltersExported += combinedFilters.Count;
                        }
                        else
                        {
                            // Write only channel-specific filters
                            var channelFile = WriteStormAudioFormat(filters, channelName, _options.EqualiserName);
                            result.ChannelFiles[$"{channelName}_filters.txt"] = channelFile;
                            
                            result.ProcessingLog.Add($"Channel {channelName}: {filters.Count} filters exported to {channelName}_filters.txt");
                            result.TotalFiltersProcessed += filters.Count;
                            result.TotalFiltersExported += filters.Count;
                        }
                    }
                }

                // Process shared sub channel (only if not combining with individual channels)
                if (sharedSub.Any() && !_options.CombineShared)
                {
                    result.SharedSubFile = WriteStormAudioFormat(sharedSub, "Shared Sub", _options.EqualiserName);
                    
                    result.ProcessingLog.Add($"Shared Sub: {sharedSub.Count} filters exported to shared_sub_filters.txt");
                    result.TotalFiltersProcessed += sharedSub.Count;
                    result.TotalFiltersExported += sharedSub.Count;
                }

                result.ProcessingLog.Add("=" + new string('=', 59));
                result.ProcessingLog.Add("Conversion complete!");
                result.ProcessingLog.Add($"Total filters processed: {result.TotalFiltersProcessed}");
                result.ProcessingLog.Add($"Total filters exported: {result.TotalFiltersExported}");
                
                // Log gain and delay settings
                if (result.GainSettings.Any())
                {
                    result.ProcessingLog.Add($"Gain settings found for {result.GainSettings.Count} channels");
                }
                if (result.DelaySettings.Any())
                {
                    result.ProcessingLog.Add($"Delay settings found for {result.DelaySettings.Count} channels");
                }
                if (result.Inversions.HasInversions)
                {
                    result.ProcessingLog.Add($"Channel inversions found: {string.Join(", ", result.Inversions.InvertedChannels)}");
                }
                else
                {
                    result.ProcessingLog.Add("No channel inversions found");
                }
            }
            catch (Exception ex)
            {
                result.ProcessingLog.Add($"Error during conversion: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Parse MSO file and extract filters by channel using two-stage parsing
        /// </summary>
        private (Dictionary<string, List<FilterData>> channels, List<FilterData> sharedSub) ParseMsoFile(string content)
        {
            var channels = new Dictionary<string, List<FilterData>>();
            var sharedSub = new List<FilterData>();

            // Stage 1: Extract channel blocks
            var channelBlocks = ExtractChannelBlocks(content);
            var sharedBlock = ExtractSharedSubBlock(content);

            // Stage 2: Parse filters within each block
            foreach (var (channelName, channelContent) in channelBlocks)
            {
                var channelFilters = ParseFiltersFromText(channelContent);
                if (channelFilters.Any())
                {
                    channels[channelName] = channelFilters;
                }
            }

            if (!string.IsNullOrEmpty(sharedBlock))
            {
                sharedSub = ParseFiltersFromText(sharedBlock);
            }

            return (channels, sharedSub);
        }

        /// <summary>
        /// Stage 1: Extract content blocks for each individual channel
        /// </summary>
        private Dictionary<string, string> ExtractChannelBlocks(string content)
        {
            var channels = new Dictionary<string, string>();

            // Find all individual channels
            var channelPattern = @"Channel: ""([^""]+)""(.*?)End Channel: ""\1""";
            var matches = Regex.Matches(content, channelPattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var channelName = match.Groups[1].Value;
                var channelContent = match.Groups[2].Value.Trim();
                channels[channelName] = channelContent;
            }

            return channels;
        }

        /// <summary>
        /// Stage 1: Extract shared sub channel block
        /// </summary>
        private string? ExtractSharedSubBlock(string content)
        {
            var sharedStart = content.IndexOf("Shared sub channel:");
            var sharedEnd = content.IndexOf("End shared sub channel");

            if (sharedStart != -1 && sharedEnd != -1)
            {
                return content.Substring(sharedStart, sharedEnd - sharedStart).Trim();
            }

            return null;
        }

        /// <summary>
        /// Stage 2: Parse all filter types from text content
        /// </summary>
        private List<FilterData> ParseFiltersFromText(string text)
        {
            var filters = new List<FilterData>();

            // Pattern to match filter blocks - each filter starts with FL## and continues until the next FL## or end
            var filterBlocks = Regex.Split(text, @"\n(?=FL\d+:)");

            foreach (var block in filterBlocks)
            {
                if (string.IsNullOrWhiteSpace(block))
                    continue;

                var lines = block.Trim().Split('\n');
                if (!lines.Any())
                    continue;

                // First line contains filter name and type
                var firstLine = lines[0];
                var filterMatch = Regex.Match(firstLine, @"(FL\d+): (.+)");

                if (!filterMatch.Success)
                    continue;

                var filterName = filterMatch.Groups[1].Value;
                var filterType = filterMatch.Groups[2].Value.Trim();

                // Check if this filter type should be included
                var shouldInclude = false;
                foreach (var includedType in _options.IncludedTypes)
                {
                    if (filterType.Contains(includedType, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldInclude = true;
                        break;
                    }
                }

                if (!shouldInclude)
                    continue;

                // Join all parameter lines for this filter
                var parameters = string.Join("\n", lines.Skip(1));

                // Parse parameters based on filter type
                if (filterType.Contains("Parametric EQ", StringComparison.OrdinalIgnoreCase))
                {
                    var filterData = ParseParametricEqParameters(filterName, filterType, parameters);
                    if (filterData != null)
                    {
                        filters.Add(filterData);
                    }
                }
                else if (filterType.Contains("All-Pass", StringComparison.OrdinalIgnoreCase))
                {
                    var filterData = ParseAllPassParameters(filterName, filterType, parameters);
                    if (filterData != null)
                    {
                        filters.Add(filterData);
                    }
                }
                // Add more filter types here as needed
            }

            return filters;
        }

        /// <summary>
        /// Parse Parametric EQ parameters
        /// </summary>
        private FilterData? ParseParametricEqParameters(string filterName, string filterType, string parameters)
        {
            var freqMatch = Regex.Match(parameters, @"Parameter ""Center freq \(Hz\)"" = ([\d.]+)");
            var gainMatch = Regex.Match(parameters, @"Parameter ""Boost \(dB\)"" = ([-\d.]+)");
            var qRbjMatch = Regex.Match(parameters, @"Parameter ""Q \(RBJ\)"" = ([\d.]+)");
            var qClassicMatch = Regex.Match(parameters, @"""Classic"" Q = ([\d.]+)");

            if (!freqMatch.Success || !gainMatch.Success || !qRbjMatch.Success)
                return null;

            if (!double.TryParse(freqMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var freq) ||
                !double.TryParse(gainMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gain) ||
                !double.TryParse(qRbjMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var qRbj))
                return null;

            var qClassic = qClassicMatch.Success && double.TryParse(qClassicMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var qClassicValue) 
                ? qClassicValue 
                : qRbj;

            // Select Q value based on q_type parameter
            var qValue = _options.QType.Equals("classic", StringComparison.OrdinalIgnoreCase) ? qClassic : qRbj;

            return new FilterData
            {
                Name = filterName,
                Type = filterType,
                Frequency = freq,
                Gain = gain,
                Q = qValue,
                QRbj = qRbj,
                QClassic = qClassic
            };
        }

        /// <summary>
        /// Parse All-Pass filter parameters
        /// </summary>
        private FilterData? ParseAllPassParameters(string filterName, string filterType, string parameters)
        {
            var freqMatch = Regex.Match(parameters, @"Parameter ""Freq of 180 deg phase \(Hz\)"" = ([\d.]+)");
            var qMatch = Regex.Match(parameters, @"Parameter ""All-pass Q"" = ([\d.]+)");

            if (!freqMatch.Success || !qMatch.Success)
                return null;

            if (!double.TryParse(freqMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var freq) ||
                !double.TryParse(qMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
                return null;

            return new FilterData
            {
                Name = filterName,
                Type = filterType,
                Frequency = freq,
                Q = q,
                Gain = 0 // All-pass filters have 0 gain
            };
        }

        /// <summary>
        /// Write filters in StormAudio format
        /// </summary>
        private string WriteStormAudioFormat(List<FilterData> filters, string channelName, string equaliserName)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Filter Settings file");
            sb.AppendLine();
            sb.AppendLine($"Dated:{DateTime.Now:yyyyMMdd}");
            sb.AppendLine();
            sb.AppendLine($"Equaliser: {equaliserName}");
            if (!string.IsNullOrEmpty(channelName))
            {
                sb.AppendLine($"Channel: {channelName}");
            }
            sb.AppendLine();

            var filterCount = 1;
            foreach (var filterData in filters)
            {
                if (filterData.Type.Contains("Parametric EQ", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"Filter {filterCount}: ON Bell Fc {filterData.Frequency.ToString("F4", CultureInfo.InvariantCulture)} Hz Gain {filterData.Gain.ToString("F5", CultureInfo.InvariantCulture)} dB Q {filterData.Q.ToString("F4", CultureInfo.InvariantCulture)}");
                    filterCount++;
                }
                else if (filterData.Type.Contains("All-Pass", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract order from filter type (e.g., "All-Pass Second-Order" -> "2")
                    var order = "2"; // Default to 2nd order
                    if (filterData.Type.Contains("Second-Order", StringComparison.OrdinalIgnoreCase))
                        order = "2";
                    else if (filterData.Type.Contains("First-Order", StringComparison.OrdinalIgnoreCase))
                        order = "1";
                    else if (filterData.Type.Contains("Third-Order", StringComparison.OrdinalIgnoreCase))
                        order = "3";
                    else if (filterData.Type.Contains("Fourth-Order", StringComparison.OrdinalIgnoreCase))
                        order = "4";

                    sb.AppendLine($"Filter {filterCount}: ON All Pass Order {order} Fc {filterData.Frequency.ToString("F4", CultureInfo.InvariantCulture)} Hz Gain 0 dB Q {filterData.Q.ToString("F6", CultureInfo.InvariantCulture)}");
                    filterCount++;
                }
                // Add more filter type conversions as needed
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parse gain and delay settings from the "Final gain and delay/distance settings" section
        /// </summary>
        private void ParseGainAndDelaySettings(string content, ConversionResult result)
        {
            try
            {
                // Find the "Final gain and delay/distance settings" section
                var finalSettingsStart = content.IndexOf("Final gain and delay/distance settings:");
                if (finalSettingsStart == -1)
                {
                    result.ProcessingLog.Add("Warning: Final gain and delay settings section not found");
                    return;
                }

                var finalSettingsEnd = content.IndexOf("Channel inversions:", finalSettingsStart);
                if (finalSettingsEnd == -1)
                {
                    // If no inversions section, find the end of the document or next major section
                    finalSettingsEnd = content.Length;
                }

                var finalSettingsText = content.Substring(finalSettingsStart, finalSettingsEnd - finalSettingsStart);

                // Parse gain settings
                var gainSection = ExtractSection(finalSettingsText, "Gain settings:", "Delay settings:");
                if (!string.IsNullOrEmpty(gainSection))
                {
                    ParseGainSettings(gainSection, result);
                }

                // Parse delay settings
                var delaySection = ExtractSection(finalSettingsText, "Delay settings:", null);
                if (!string.IsNullOrEmpty(delaySection))
                {
                    ParseDelaySettings(delaySection, result);
                }
            }
            catch (Exception ex)
            {
                result.ProcessingLog.Add($"Warning: Error parsing gain and delay settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse channel inversions from the "Channel inversions" section
        /// </summary>
        private void ParseChannelInversions(string content, ConversionResult result)
        {
            try
            {
                var inversionsStart = content.IndexOf("Channel inversions:");
                if (inversionsStart == -1)
                {
                    result.ProcessingLog.Add("Warning: Channel inversions section not found");
                    return;
                }

                var inversionsText = content.Substring(inversionsStart);
                var lines = inversionsText.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();

                if (lines.Count > 1)
                {
                    // Skip the header line "Channel inversions:"
                    var inversionLines = lines.Skip(1).ToList();

                    foreach (var line in inversionLines)
                    {
                        if (line.Equals("No inversions", StringComparison.OrdinalIgnoreCase))
                        {
                            // No inversions found
                            break;
                        }
                        else if (line.Contains("invert", StringComparison.OrdinalIgnoreCase))
                        {
                            // Parse inverted channels - handle formats like "FL: Invert" or "FL invert"
                            var channelMatch = Regex.Match(line, @"(\w+)\s*:?\s*invert", RegexOptions.IgnoreCase);
                            if (channelMatch.Success)
                            {
                                result.Inversions.InvertedChannels.Add(channelMatch.Groups[1].Value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.ProcessingLog.Add($"Warning: Error parsing channel inversions: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract a section of text between start and end markers
        /// </summary>
        private string ExtractSection(string text, string startMarker, string? endMarker)
        {
            var startIndex = text.IndexOf(startMarker);
            if (startIndex == -1) return string.Empty;

            var startPos = startIndex + startMarker.Length;
            
            if (endMarker != null)
            {
                var endIndex = text.IndexOf(endMarker, startPos);
                if (endIndex != -1)
                {
                    return text.Substring(startPos, endIndex - startPos).Trim();
                }
            }

            return text.Substring(startPos).Trim();
        }

        /// <summary>
        /// Parse gain settings from the gain section text
        /// </summary>
        private void ParseGainSettings(string gainText, ConversionResult result)
        {
            var lines = gainText.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l));

            foreach (var line in lines)
            {
                // Parse lines like "FL gain: 0.00 dB"
                var gainMatch = Regex.Match(line, @"(\w+)\s+gain:\s*([-\d.]+)\s*dB", RegexOptions.IgnoreCase);
                if (gainMatch.Success)
                {
                    var channelName = gainMatch.Groups[1].Value;
                    if (double.TryParse(gainMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gainValue))
                    {
                        result.GainSettings.Add(new ChannelGainSetting
                        {
                            ChannelName = channelName,
                            GainValue = gainValue
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Parse delay settings from the delay section text
        /// </summary>
        private void ParseDelaySettings(string delayText, ConversionResult result)
        {
            var lines = delayText.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l));

            foreach (var line in lines)
            {
                // Parse lines like "FL delay: 0.00 msec"
                var delayMatch = Regex.Match(line, @"(\w+)\s+delay:\s*([-\d.]+)\s*msec", RegexOptions.IgnoreCase);
                if (delayMatch.Success)
                {
                    var channelName = delayMatch.Groups[1].Value;
                    if (double.TryParse(delayMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var delayValue))
                    {
                        result.DelaySettings.Add(new ChannelDelaySetting
                        {
                            ChannelName = channelName,
                            DelayValue = delayValue
                        });
                    }
                }
            }
        }
    }
}
