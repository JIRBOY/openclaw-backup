# OpenClaw Backup Tool

A .NET 8 command-line utility for backing up OpenClaw configuration and workspace directories.

## Features

- **Zero external dependencies** — Uses built-in .NET `System.Formats.Tar` + `GZipStream` for compression
- **Automatic source directory detection** — Finds `.openclaw` config and `OpenClaw` workspace directories automatically
- **Symlink-aware** — Skips symbolic links and junction points to avoid bloated archives
- **Smart exclusion rules** — Excludes cache files, temporary files, node_modules, `.git`, and other non-essential directories
- **Service management** — Automatically stops the OpenClaw gateway service when file locks are detected, retries backup, then restarts the service
- **Backup rotation** — Configurable retention policy to keep only the most recent N backups
- **Dry-run mode** — Preview what will be backed up without creating archives

## Usage

```bash
# Run backup
openclaw_backup.exe

# Dry-run (preview files without creating archive)
openclaw_backup.exe --dry-run

# Custom config file
openclaw_backup.exe --config D:\path\to\openclaw_backup.xml

# Default config location (Windows)
%APPDATA%\OpenClaw_Backup\openclaw_backup.xml

# Show help
openclaw_backup.exe --help
```

## Configuration

Edit `openclaw_backup.xml` (located in the same directory as the executable):

```xml
<?xml version="1.0" encoding="utf-8"?>
<OpenclawBackup>
  <ConfigDirectory></ConfigDirectory>          <!-- Auto-detected if empty -->
  <WorkspaceDirectory>D:\Personal\OpenClaw</WorkspaceDirectory>  <!-- Auto-detected -->
  <OpenclawCommandPath></OpenclawCommandPath>  <!-- Cached automatically -->
  <OutputDirectory>D:\Personal\Archive</OutputDirectory>
  <FileNamePrefix>openclaw-backup</FileNamePrefix>
  <MaxBackups>30</MaxBackups>                  <!-- 0 = unlimited -->
</OpenclawBackup>
```

## Output

Backups are saved as `openclaw-backup-YYYYMMDD-HHmmss.tar.gz` containing both source directories with absolute paths preserved for easy restoration.

## Excluded by Default

**File extensions:** `*.log`, `*.tmp`, `*.pyc`, `*.pyo`, `*.pid`

**Directories:** `tmp`, `temp`, `backup`, `logs`, `browser`, `node_modules`, `.git`, `__pycache__`, `.venv`, `venv`, `.next`, `.cache`, `dist`, `build`, `extensions`, `agents`, `tasks`

**Symlinks and junction points** are automatically skipped.

## Build

```bash
dotnet build -c Release
```

Output: `bin/Release/net8.0/openclaw_backup.exe`

## License

MIT
