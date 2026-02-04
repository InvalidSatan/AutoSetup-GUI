# University Auto Setup v2.0

Professional-grade Windows machine setup application for Appalachian State University IT department.

## Overview

University Auto Setup automates essential post-imaging configuration tasks:
- **System Information Collection** - Comprehensive hardware and software details for asset management
- **Group Policy Update** - Refresh all Group Policies with retry logic
- **SCCM Client Actions** - Trigger policy, inventory, and update actions
- **Dell Command Update** - Auto-install and run Dell driver/firmware updates
- **Image Verification** - Validate Windows activation, domain, SCCM, disk space, network, and BitLocker

## Requirements

- Windows 10/11
- .NET 8 Desktop Runtime
- Administrative privileges
- Network access to P:\ drive (optional, for logging)

## Quick Start

1. Run `AutoSetup-GUI.bat` from the network share
2. The launcher will request administrative privileges
3. Select desired tasks on the Tasks page
4. Click "Run All Selected" or run individual tasks

## Features

### Dashboard
- System summary with key asset information
- Quick status overview of all components
- One-click setup initiation

### System Information
- Full hardware and software details
- One-click clipboard copy (formatted for tickets)
- Export to Text, JSON, or CSV formats

### Tasks
- **Group Policy Update** - gpupdate /force with automatic retry
- **SCCM Client Actions** - All 5 standard actions with individual status tracking
- **Dell Command Update** - Install, configure, scan, and apply updates
- **Image Verification** - 7 post-imaging checks

### Logging
- Dual-location logging (local + P:\ drive)
- Real-time log viewer with filtering
- Structured folders: `{ServiceTag}_{ComputerName} - {Date}`
- HTML summary reports with university branding

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| F5 | Refresh data |
| Ctrl+A | Run all selected tasks |
| Ctrl+C | Copy system info |
| Ctrl+S | Export/save logs |
| Ctrl+Q | Quit application |

## Configuration

Edit `appsettings.json` to customize:
- Dell Command Update installer path
- SCCM action timeout and repair settings
- Image check thresholds
- Log retention and paths

## Deployment

### Network Share Structure
```
\\server\share\UniversityAutoSetup\v2.0\
├── AutoSetup-GUI.exe
├── AutoSetup-GUI.bat
├── appsettings.json
└── README.txt
```

### Building from Source
```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false
```

## Log Files

**Local:** `C:\Temp\UniversityAutoSetup\Logs\`
**Network:** `P:\UniversityAutoSetup\Logs\{ServiceTag}_{ComputerName} - {Date}\`

Files generated:
- `UniversityAutoSetup_v2.0.0_{timestamp}.log` - Main log
- `SystemInfo_{timestamp}.txt` - Asset details
- `DCU_{timestamp}.log` - Dell Command Update output
- `Summary_{timestamp}.html` - HTML report

## Contact

- **Name:** Alex Guill
- **Email:** guillra@appstate.edu
- **Organization:** Appalachian State University

## License

Copyright (c) Appalachian State University. All rights reserved.
