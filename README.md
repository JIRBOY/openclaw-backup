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

Edit `openclaw_backup.xml` (located at `%APPDATA%\OpenClaw_Backup\openclaw_backup.xml`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<OpenclawBackup>
  <ConfigDirectory></ConfigDirectory>          <!-- Auto-detected if empty -->
  <WorkspaceDirectory>D:\Personal\OpenClaw</WorkspaceDirectory>  <!-- Auto-detected -->
  <OpenclawCommandPath></OpenclawCommandPath>  <!-- Cached automatically -->
  <OutputDirectory>D:\Personal\Archive</OutputDirectory>
  <FileNamePrefix>openclaw-backup</FileNamePrefix>
  <MaxBackups>30</MaxBackups>                  <!-- 0 = unlimited -->

  <!-- 排除规则 - 修改后无需重新编译程序 -->
  <ExcludedExtensions>
    <Extension>log</Extension>
    <Extension>tmp</Extension>
    <Extension>pyc</Extension>
    <Extension>pyo</Extension>
    <Extension>pid</Extension>
    <Extension>bak</Extension>
    <Extension>swp</Extension>
    <Extension>swo</Extension>
    <Extension>cache</Extension>
  </ExcludedExtensions>
  <ExcludedFolders>
    <Folder>tmp</Folder>
    <Folder>temp</Folder>
    <Folder>backup</Folder>
    <Folder>logs</Folder>
    <Folder>browser</Folder>
    <Folder>node_modules</Folder>
    <Folder>.git</Folder>
    <Folder>__pycache__</Folder>
    <Folder>.venv</Folder>
    <Folder>venv</Folder>
    <Folder>.next</Folder>
    <Folder>.cache</Folder>
    <Folder>dist</Folder>
    <Folder>build</Folder>
    <Folder>extensions</Folder>
    <Folder>agents</Folder>
    <Folder>tasks</Folder>
    <!-- OpenClaw 特有 -->
    <Folder>skills-backup</Folder>
    <Folder>.browser-profile</Folder>
    <Folder>.Trash</Folder>
    <Folder>.clawhub</Folder>
  </ExcludedFolders>
  <ExcludedFiles>
    <File>.DS_Store</File>
    <File>Thumbs.db</File>
    <File>desktop.ini</File>
  </ExcludedFiles>
</OpenclawBackup>
```

**注意：** 留空的排除列表将使用程序内置默认值。如需自定义，添加对应条目即可。支持通配符 `*` 匹配文件夹名（如 `skills-backup-*`）。

## Output

Backups are saved as `openclaw-backup-YYYYMMDD-HHmmss.tar.gz` containing both source directories with absolute paths preserved for easy restoration.

## Excluded by Default

**File extensions:** `*.log`, `*.tmp`, `*.pyc`, `*.pyo`, `*.pid`, `*.bak`, `*.swp`, `*.swo`, `*.cache`

**Directories:** `tmp`, `temp`, `backup`, `logs`, `browser`, `node_modules`, `.git`, `__pycache__`, `.venv`, `venv`, `.next`, `.cache`, `dist`, `build`, `extensions`, `agents`, `tasks`, `skills-backup`, `.browser-profile`, `.Trash`, `.clawhub`

**Files:** `.DS_Store`, `Thumbs.db`, `desktop.ini`

**Symlinks and junction points** are automatically skipped.

> All exclusion rules are now configurable in `openclaw_backup.xml`. See [Configuration](#configuration) above.

## Build

```bash
dotnet build -c Release
```

Output: `bin/Release/net8.0/openclaw_backup.exe`

## License

MIT
