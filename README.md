# University Auto Setup v2.0

A professional-grade Windows machine setup application for Appalachian State University IT department. This WPF application streamlines post-imaging configuration by automating Group Policy updates, SCCM client actions, Dell driver updates, and image verification checks.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat&logo=windows)
![License](https://img.shields.io/badge/License-Internal-gray)

---

## Features

### System Information Collection
- **Hardware Details**: Service Tag, Manufacturer, Model, BIOS Version
- **Network Information**: Ethernet MAC, WiFi MAC, IP Addresses
- **OS Details**: Windows Version, Build, Architecture, Last Boot Time
- **Domain Information**: Domain membership, OU path
- **SCCM Status**: Client version, Site Code, Management Point
- **One-Click Copy**: Formatted output for ticketing systems
- **Export Options**: CSV, JSON, or plain text formats

### Automated Setup Tasks
| Task | Description |
|------|-------------|
| **Group Policy Update** | Executes `gpupdate /force` with retry logic |
| **SCCM Client Actions** | Triggers 5 essential Configuration Manager actions |
| **Dell Command Update** | Auto-installs DCU and applies driver/BIOS updates |
| **Image Verification** | Runs 7 post-image health checks |

### Real-Time Progress Tracking
- Individual task status badges update in real-time
- Visual status indicators: Pending → Running → Success/Warning/Error
- Duration tracking for each completed task
- Overall progress bar with detailed status messages

### Image Verification Checks
| Check | Pass Criteria |
|-------|---------------|
| Windows Activation | Licensed status confirmed |
| Domain Join | Joined to .appstate.edu domain |
| SCCM Client Health | CCMExec service running, WMI accessible |
| Disk Space | Minimum 20GB free on C:\ |
| Network Connectivity | Internet access + P:\ drive available |
| Pending Reboot | No pending reboot flags |
| BitLocker Status | Protection enabled |

### Comprehensive Logging
- **Local Logs**: `C:\Temp\UniversityAutoSetup\Logs\`
- **Network Logs**: `P:\UniversityAutoSetup\Logs\{ServiceTag}_{ComputerName} - {Date}\`
- **PDF Reports**: Branded summary reports auto-generated and opened on completion
- **Real-Time Log Viewer**: Built-in filterable log viewer with search

---

## Requirements

### System Requirements
- Windows 10/11 (64-bit)
- .NET 8 Desktop Runtime
- Administrative privileges
- Network access to P:\ drive (for network logging)

### For Dell Systems
- Dell Command Update will be automatically installed if missing
- Supported on Dell laptops and desktops only

---

## Installation

### Network Share Deployment (Recommended)
1. Copy the published application to a network share:
   ```
   \\server\share\UniversityAutoSetup\v2.0\
   ├── AutoSetup-GUI.exe
   ├── AutoSetup-GUI.bat
   ├── appsettings.json
   └── README.txt
   ```

2. Run from the network share using the batch launcher (auto-elevates to admin)

### Local Installation
1. Ensure .NET 8 Desktop Runtime is installed
2. Copy the application folder to the target machine
3. Run `AutoSetup-GUI.bat` or `AutoSetup-GUI.exe` as Administrator

---

## Usage

### Quick Start
1. Launch the application with administrative privileges
2. Click **"Start Complete Setup"** on the Dashboard
3. Monitor real-time progress on the Tasks tab
4. Review the auto-generated PDF report

### Running Individual Tasks
Navigate to the **Tasks** tab to:
- Select/deselect specific tasks
- Run individual tasks using the **Run** button next to each
- Run all selected tasks with **Run All Tasks**

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| `F5` | Refresh system information |
| `Ctrl+C` | Copy system info to clipboard |
| `Ctrl+Q` | Quit application |

---

## Configuration

Configuration is managed via `appsettings.json`:

```json
{
  "Application": {
    "Version": "2.0.0",
    "Name": "University Auto Setup"
  },
  "Logging": {
    "LocalPath": "C:\\Temp\\UniversityAutoSetup\\Logs",
    "NetworkPath": "P:\\UniversityAutoSetup\\Logs"
  },
  "DellCommandUpdate": {
    "InstallerUNC": "\\\\server\\share\\Dell\\DCU\\DCU_Setup.exe",
    "MaxRetries": 3
  },
  "SCCM": {
    "ActionTimeoutSeconds": 120,
    "RepairOnFailure": true
  },
  "ImageChecks": {
    "MinDiskSpaceGB": 20,
    "BitLockerRequired": true
  }
}
```

### Key Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `Logging.NetworkPath` | UNC path for network log storage | `P:\UniversityAutoSetup\Logs` |
| `DellCommandUpdate.InstallerUNC` | Network path to DCU installer | Configure for your environment |
| `SCCM.ActionTimeoutSeconds` | Timeout per SCCM action | 120 seconds |
| `ImageChecks.MinDiskSpaceGB` | Minimum free disk space | 20 GB |

---

## Architecture

### Technology Stack
- **Framework**: .NET 8 WPF (Windows Presentation Foundation)
- **Pattern**: Service-based architecture with dependency injection
- **UI**: Modern card-based design with App State branding
- **Logging**: Serilog with file and real-time sinks
- **PDF Generation**: QuestPDF for branded reports

### Project Structure
```
AutoSetup-GUI/
├── src/AutoSetupGUI/
│   ├── Views/              # XAML UI pages
│   ├── Models/             # Data models
│   ├── Services/           # Business logic
│   │   ├── Interfaces/     # Service contracts
│   │   └── Implementations/# Service implementations
│   ├── Infrastructure/     # WMI, Registry, Process helpers
│   ├── Themes/             # App State branding & styles
│   └── Resources/          # Images, icons
├── config/                 # Configuration files
└── launcher/               # Batch launcher for admin elevation
```

### Key Services
| Service | Purpose |
|---------|---------|
| `TaskOrchestrator` | Coordinates all setup tasks with real-time progress events |
| `SystemInfoService` | Collects hardware/software information via WMI (with caching) |
| `SCCMService` | Executes Configuration Manager client actions |
| `DellUpdateService` | Manages Dell Command Update installation and execution |
| `ImageCheckService` | Runs post-image verification checks |
| `ReportService` | Generates branded PDF summary reports |
| `LoggingService` | Dual-location logging (local + network) |

---

## Performance Optimizations

### WMI Caching
System information queries are cached for the session lifetime, reducing redundant WMI calls. Use the **Refresh** button to clear cache and fetch fresh data.

### PDF Reports
Reports are generated as PDF (using QuestPDF) instead of HTML for faster opening and better cross-system compatibility. Reports auto-open on task completion.

### Framework-Dependent Deployment
The application uses framework-dependent deployment, keeping the executable small (~15-40MB) for efficient network share deployment.

---

## SCCM Client Actions

The following Configuration Manager client actions are executed:

| Action | Schedule ID |
|--------|-------------|
| Machine Policy Retrieval | `{00000000-0000-0000-0000-000000000021}` |
| Machine Policy Evaluation | `{00000000-0000-0000-0000-000000000022}` |
| Hardware Inventory | `{00000000-0000-0000-0000-000000000001}` |
| Software Updates Scan | `{00000000-0000-0000-0000-000000000113}` |
| Software Updates Deployment | `{00000000-0000-0000-0000-000000000108}` |

---

## Dell Command Update

### Automatic Installation
If Dell Command Update is not installed on a Dell system, the application will:
1. Copy the installer from the configured network share
2. Install DCU silently
3. Configure with: `autoSuspendBitLocker=enable`, `userConsent=disable`

### Exit Code Handling
| Exit Code | Meaning | Action |
|-----------|---------|--------|
| 0, 2, 3 | Success | Continue |
| 1 | No updates available | Success (no action needed) |
| 5, 7, 8 | Temporary failure | Retry (up to 3 times) |

---

## Branding

The application uses Appalachian State University branding:

| Element | Color | Hex |
|---------|-------|-----|
| Success | Grass Green | `#69AA61` |
| Warning | Dark Gold | `#D7A527` |
| Error | Brick Orange | `#C6602A` |
| Running/Info | Lake Blue | `#03659C` |
| Primary Text | Near Black | `#010101` |
| Accent | App State Gold | `#FFCC00` |

---

## Building from Source

### Prerequisites
- Visual Studio 2022 or later
- .NET 8 SDK
- Windows 10/11 SDK

### Build Commands
```powershell
# Restore dependencies
dotnet restore src/AutoSetupGUI/AutoSetupGUI.csproj

# Build Release
dotnet build src/AutoSetupGUI/AutoSetupGUI.csproj -c Release

# Publish single-file executable
dotnet publish src/AutoSetupGUI/AutoSetupGUI.csproj -c Release -r win-x64
```

### Output Location
Published files will be in:
```
src/AutoSetupGUI/bin/Release/net8.0-windows/win-x64/publish/
```

---

## Troubleshooting

### Common Issues

**"Application requires administrator privileges"**
- Run the application using `AutoSetup-GUI.bat` or right-click → Run as Administrator

**"SCCM Client not detected"**
- Verify the SCCM client is installed (`C:\Windows\CCM\CcmExec.exe`)
- Check that the CCMExec service is running

**"Dell updates skipped - Not a Dell system"**
- Dell Command Update only runs on Dell hardware
- The application auto-detects manufacturer via WMI

**"Network logs not saved"**
- Verify P:\ drive is mapped and accessible
- Check write permissions to the network path
- Logs will still save locally if network is unavailable

**"PDF report not opening"**
- Ensure a PDF reader is installed (Adobe Acrobat, Edge, etc.)
- Check the local log folder for the generated report

---

## Version History

### v2.0.0 (Current)
- Complete rewrite from PowerShell/Windows Forms to C# WPF
- Modern card-based UI with App State branding
- Real-time individual task status updates
- WMI caching for improved performance
- PDF report generation (faster than HTML)
- Built-in log viewer with filtering
- Framework-dependent deployment for smaller file size

### v1.0.0 (Legacy)
- Original PowerShell script with Windows Forms GUI
- Basic task execution without real-time feedback

---

## Support

**Author**: Alex Guill
**Email**: guillra@appstate.edu
**Organization**: Appalachian State University - Information Technology Services

---

## License

Internal use only - Appalachian State University

Copyright (c) 2024 Appalachian State University. All rights reserved.
