# OpenClaw 自动备份工具 - 项目说明与设计方案

## Context

本项目是一个 OpenClaw 自动备份工具，使用 .NET 8 内置的 `System.Formats.Tar` + `GZipStream` 进行压缩。

- 开发语言：C#，.NET 8 平台
- 编译产物：`openclaw_backup.exe`
- 配置文件：`openclaw_backup.xml`，默认存储在 `%APPDATA%\OpenClaw_Backup\openclaw_backup.xml`
- 压缩格式：`.tar.gz`（零外部依赖，天然不跟随符号链接）
- GitHub: https://github.com/JIRBOY/openclaw-backup

## 需要备份的源目录

| 目录类型 | 默认路径 | 找不到时行为 |
|---------|---------|------------|
| 配置目录 | `%USERPROFILE%\.openclaw` | 自动搜索常见位置 |
| 工作空间 | `D:\Personal\OpenClaw` | 自动搜索常见位置 |

所有源目录合并到单个 `.tar.gz` 文件中，保留绝对路径以便恢复。

## 排除规则

备份时排除以下文件和文件夹：

**文件扩展名：** `*.log`, `*.tmp`, `*.pyc`, `*.pyo`, `*.pid`

**文件夹名：** `tmp`, `temp`, `backup`, `logs`, `browser`, `node_modules`, `.git`, `__pycache__`, `.venv`, `venv`, `.next`, `.cache`, `.DS_Store`, `Thumbs.db`, `desktop.ini`, `dist`, `build`, `extensions`, `agents`, `tasks`

**符号链接/联接点：** 自动跳过（`EnumerationOptions.AttributesToSkip = FileAttributes.ReparsePoint`）

## 项目结构

```
D:\Openclaw自定义备份工具\
├── openclaw_backup.csproj          # .NET 8 控制台项目
├── Program.cs                      # 程序入口 + 权限处理
├── BackupConfig.cs                 # XML 配置加载/保存
├── BackupRunner.cs                 # tar.gz 备份执行核心逻辑
├── ExclusionFilter.cs              # 排除规则管理 + 符号链接检测
├── ServiceManager.cs               # OpenClaw 网关服务启停管理
├── PrivilegeHelper.cs              # 权限检测与 UAC 自动提权
└── bin\Release\net8.0\
    └── openclaw_backup.exe         # 编译产物
```

## 配置文件 openclaw_backup.xml

存储位置：`%APPDATA%\OpenClaw_Backup\openclaw_backup.xml`（程序首次运行时自动创建目录）

```xml
<?xml version="1.0" encoding="utf-8"?>
<OpenclawBackup>
  <ConfigDirectory></ConfigDirectory>          <!-- 配置目录：自动检测 -->
  <WorkspaceDirectory>D:\Personal\OpenClaw</WorkspaceDirectory>  <!-- 工作空间：自动检测 -->
  <OpenclawCommandPath></OpenclawCommandPath>  <!-- openclaw 命令路径：缓存 -->
  <OutputDirectory>D:\Personal\Archive</OutputDirectory>
  <FileNamePrefix>openclaw-backup</FileNamePrefix>
  <MaxBackups>30</MaxBackups>                  <!-- 保留最近 N 个，0=不限制 -->
</OpenclawBackup>
```

## 核心功能

### 命令行参数
```
openclaw_backup.exe [--config <path>] [--dry-run] [--help]
```

### 备份流程
1. 解析参数，加载/创建 XML 配置
2. 自动检测配置目录和工作空间目录
3. 合并所有源目录到单个 `openclaw-backup-YYYYMMDD-HHmmss.tar.gz`
4. 遍历时跳过符号链接和排除规则
5. tar 内部路径保留绝对路径（如 `C:\Users\...\`）
6. 如文件被锁定（SQLite）→ `openclaw gateway stop` → 重试 → `openclaw gateway start`
7. 权限不足 → UAC 自动提权
8. 清理过期备份（按 MaxBackups）

### openclaw 命令查找顺序
1. 配置文件缓存路径（`OpenclawCommandPath`）
2. 系统 PATH 中的 `openclaw` / `openclaw.cmd`
3. `%USERPROFILE%\.openclaw\bin\openclaw`
4. `%LOCALAPPDATA%\Programs\OpenClaw\openclaw`
5. 找不到则提示用户手动输入并缓存到配置文件

### 权限处理
- 配置文件存储在 `%APPDATA%` 而非程序目录，避免 `Program Files` 写入权限问题
- 检测到其他操作权限不足时，使用 `ProcessStartInfo.Verb = "runas"` UAC 提权重启自身
- 用户拒绝提权 → 提示 "请以管理员身份运行" 并退出

### 压缩实现
- 使用 .NET 8 `System.Formats.Tar.TarWriter` + `System.IO.Compression.GZipStream`
- 零外部依赖，跨平台兼容
- 遍历时 `AttributesToSkip = FileAttributes.ReparsePoint` 跳过符号链接和联接点
- 每个源目录通过 `GetDirLabel()` 识别为 "config" 或 "workspace"
