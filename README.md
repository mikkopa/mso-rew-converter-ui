# MSO to REW Filter Converter - Web UI

A modern web-based user interface for converting Multi-Sub Optimizer (MSO) filter configurations to REW (Room EQ Wizard) / StormAudio compatible format. Built with Blazor WebAssembly for client-side processing.

## 🌟 Features

### Core Functionality
- **Web-based Interface**: No installation required - runs entirely in your browser
- **File Upload Support**: Drag and drop or select MSO filter report files (.txt)
- **Real-time Validation**: Instant feedback on MSO file format
- **Live Preview**: See conversion results before downloading
- **Multiple Download Options**: Download individual files or all files at once

### Filter Processing
- **Two-stage Parsing**: Accurately extracts filters from MSO channel blocks
- **Multiple Filter Types**: Supports Parametric EQ and All-Pass filters
- **Q Value Selection**: Choose between RBJ or Classic Q values
- **Filter Type Control**: Enable/disable specific filter types
- **Flexible Output**: Separate files per channel or combined with shared filters

### Channel Settings Display
- **Gain Settings**: View final gain values for each channel
- **Delay Settings**: Display delay/distance settings per channel  
- **Channel Inversions**: Show which channels have polarity inversion
- **Professional Format**: Generates REW/StormAudio compatible filter files

## 🚀 Getting Started

### Access the Application
Visit the web application at: https://purple-glacier-033fcb503.1.azurestaticapps.net/

Or run locally:
1. Clone this repository
2. Install .NET 9.0 or later
3. Run `dotnet run` in the Client directory
4. Open your browser to `https://localhost:5000`

### Using the Converter

1. **Get MSO Filter Report**:
   - In MSO, select "Show Filter Report (Abbreviated)"
   - Save or copy the report content

2. **Load Data**:
   - Use the file selector to upload a .txt file, or
   - Paste the MSO content directly into the text area

3. **Configure Options**:
   - Choose Q value type (RBJ recommended for StormAudio)
   - Select filter types to include
   - Set equalizer name for output files
   - Enable "Combine Shared" if needed

4. **Convert & Download**:
   - Click "Convert" to process the filters
   - Add manual filter to all channels or individual channels. These are added before filters from MSO content.
   - Download individual files or all files at once

## 📊 Supported Filter Types

### Currently Supported
- **Parametric EQ**: Converted to Bell filters with configurable Q values
- **All-Pass Filters**: Converted to All Pass filters (various orders)

### Channel Settings Extracted
- **Gain Block**: Final gain values displayed separately
- **Delay Block**: Final delay values displayed separately  
- **Polarity Inversion**: Channel inversions displayed separately

## 💾 Input Format

The tool expects MSO "Filter Report (Abbreviated)" format:

```
Channel: "FL"
FL1: Parametric EQ (RBJ)
Parameter "Center freq (Hz)" = 52.9284
Parameter "Boost (dB)" = -2.56499
Parameter "Q (RBJ)" = 11.0387
"Classic" Q = 12.7951

FL9: All-Pass Second-Order
Parameter "Freq of 180 deg phase (Hz)" = 32.1576
Parameter "All-pass Q" = 0.500044
End Channel: "FL"

Final gain and delay/distance settings:
Gain settings:
FL gain: -2.34 dB
FR gain: -1.89 dB

Delay settings:
FL delay: 12.45 msec
FR delay: 8.73 msec

Channel inversions:
FL: Invert
No other inversions
```

## 📁 Output Format

### Filter Files
Generated files are compatible with REW and StormAudio processors:

```
Filter Settings file

Dated:20250819

Equaliser: StormAudio
Channel: FL

Filter 1: ON Bell Fc 52.9284 Hz Gain -2.56499 dB Q 11.0387
Filter 2: ON All Pass Order 2 Fc 32.1576 Hz Gain 0 dB Q 0.500044
```

### Output Files Structure

#### Default Mode (Separate Files)
- `FL_filters.txt` - Front Left channel
- `FR_filters.txt` - Front Right channel  
- `RL_filters.txt` - Rear Left channel
- `RR_filters.txt` - Rear Right channel
- `shared_sub_filters.txt` - Shared subwoofer filters

#### Combined Mode
- `FL_filters.txt` - Shared + Front Left filters
- `FR_filters.txt` - Shared + Front Right filters
- Each channel file contains shared subwoofer filters followed by channel-specific filters

## ⚙️ Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| Equalizer Name | Name used in output files | StormAudio |
| Q Value Type | RBJ (recommended) or Classic | RBJ |
| Include Parametric EQ | Process Parametric EQ filters | ✓ |
| Include All-Pass | Process All-Pass filters | ✓ |
| Combine Shared | Merge shared filters with channels | ✗ |

## 🔧 Technical Details

### Architecture
- **Frontend**: Blazor WebAssembly (.NET 9.0)
- **Processing**: Client-side C# with regex-based parsing
- **Storage**: Browser localStorage for settings persistence
- **File Handling**: JavaScript FileReader API integration

### Q Value Types
- **RBJ Q**: Uses `Parameter "Q (RBJ)"` values from MSO output (StormAudio compatible)
- **Classic Q**: Uses `"Classic" Q` values from MSO output

### Two-Stage Parsing
1. **Stage 1**: Extract channel blocks using regex pattern matching
2. **Stage 2**: Parse individual filters within each block
3. **Type Filtering**: Include only selected filter types
4. **Format Conversion**: Convert to REW/StormAudio format
5. **File Generation**: Create downloadable files

## 🐛 Troubleshooting

### Common Issues

**No filters found**
- Check that input file contains proper MSO "Filter Report (Abbreviated)" format
- Verify Parametric EQ and/or All-Pass filters are enabled
- Ensure channel boundaries are properly formatted (`Channel: "XX"` to `End Channel: "XX"`)

**Missing channel settings**
- Check for "Final gain and delay/distance settings:" section in MSO report
- Verify "Channel inversions:" section exists

**Incorrect Q values**
- Try switching between RBJ and Classic Q value types
- Both Q types should be available in MSO output

### Debug Information

The conversion log shows:
- Filter types being processed
- Number of filters found per channel
- Channel settings detected
- Total filters processed vs exported

## 🏗️ Development

### Building from Source
```bash
git clone https://github.com/mikkopa/mso-rew-converter-ui.git
cd mso-rew-converter-ui
dotnet restore
dotnet build
dotnet run --project Client
```

### Project Structure
```
Client/             # Blazor WebAssembly app
├── Components/     # Blazor components
├── Services/       # Conversion logic
├── Layout/         # App layout
└── wwwroot/        # Static assets
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing & Feedback

We welcome your feedback and contributions! 

**Found a bug or have a feature request?**
Please [open an issue](https://github.com/mikkopa/mso-rew-converter-ui/issues/new) to let us know. Your feedback helps improve the tool for everyone.

**Want to contribute?**
- Fork the repository
- Create a feature branch
- Submit a pull request

## 🙏 Acknowledgments

- **Multi-Sub Optimizer (MSO)** by Andy C - The source of filter configurations
- **Room EQ Wizard (REW)** by John Mulcahy - Compatible output format
- **StormAudio** - Filter format specifications and PEQ compatibility

## 📈 Version History

- **v1.0**: Initial Blazor WebAssembly release
- **v1.0.1**: Added option to add manual filters to result files. Added option to show delay in milliseconds, meters or feets and to add an offset to delay values.

---

💡 **Tip**: For StormAudio processors, use RBJ Q values for best compatibility with the built-in PEQ modules.
