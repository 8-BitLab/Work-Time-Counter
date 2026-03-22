# Work Time Counter

A Windows desktop application for tracking work hours with real-time Firebase synchronization and local LiteDB storage.

Built with C# and Windows Forms (.NET Framework 4.7.2).

## Features

- **Start / Stop / Pause** — one-click work timer with live elapsed time display
- **Firebase Sync** — real-time data synchronization across devices using Firebase Realtime Database
- **Local Storage** — offline-first with LiteDB embedded database, syncs when connected
- **Online Users** — see who is currently working with live heartbeat/presence indicators
- **Live Logging** — real-time work session signals visible to the team
- **Reports** — daily and monthly work time summaries with data grid view
- **PDF Export** — generate printable work time reports via iTextSharp
- **Dark / Light Theme** — switchable UI theme
- **Auto-Update** — built-in version checker to keep the app up to date
- **Debug Console** — dedicated debug form for diagnostics

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Language | C# (.NET Framework 4.7.2) |
| UI | Windows Forms (WinForms) |
| Local DB | LiteDB 5.0 |
| Cloud Sync | Firebase Realtime Database |
| PDF | iTextSharp 5.5 |
| JSON | Newtonsoft.Json 13.0 |
| Crypto | BouncyCastle 2.4 |
| Reactive | System.Reactive 6.0 |

## Getting Started

### Prerequisites

- Windows 10 or later
- Visual Studio 2019+ with .NET desktop development workload
- .NET Framework 4.7.2

### Build from Source

1. Clone the repository
   ```
   git clone https://github.com/8-BitLab/Work-Time-Counter.git
   ```
2. Open `Work Time Counter.sln` in Visual Studio
3. Restore NuGet packages (right-click solution > Restore NuGet Packages)
4. Build and run (F5)

### Download

Get the latest release from the [Releases](https://github.com/8-BitLab/Work-Time-Counter/releases) page.

## Project Structure

```
Work-Time-Counter/
  Work Time Counter/
    Program.cs              # Application entry point
    Form1.cs                # Main form — timer, logging, Firebase sync, reports
    DebugForm.cs            # Debug/diagnostics window
    App.config              # Application configuration
    packages.config         # NuGet dependencies
  Work Time Counter.sln     # Visual Studio solution
```

## License

This project is part of the [8-Bit Lab Engineering](https://8bitlab.de) open source programme.

## Author

**8-Bit Lab Engineering** — [8bitlab.de](https://8bitlab.de)
