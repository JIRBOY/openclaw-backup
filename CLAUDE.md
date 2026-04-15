# OpenClaw 自动备份工具 - 项目说明与设计方案

## Context

本项目是一个 OpenClaw 自动备份工具，使用 .NET 8 内置的 `System.Formats.Tar` + `GZipStream` 进行压缩。

- 开发语言：C#，.NET 8 平台
- 编译产物：`openclaw_backup.exe`
- 配置文件：`openclaw_backup.xml`
- 压缩格式：`.tar.gz`（零外部依赖，天然不跟随符号链接）

## 需要备份的源目录

| 目录类型 | 默认路径 | 找不到时行为 |
|---------|---------|------------|
| 配置目录 | `%USERPROFILE%\.openclaw` | 自动搜索常见位置 |
| 工作空间 | `D:\Personal\OpenClaw` | 自动搜索常见位置 |

每个目录分别压缩为独立的 `.tar.gz` 文件，保留绝对路径以便恢复。

## 排除规则

备份时排除以下文件和文件夹：

**文件扩展名：** `*.log`, `*.tmp`, `*.pyc`, `*.pyo`, `*.pid`

**文件夹名：** `tmp`, `temp`, `backup`, `logs`, `brower`, `node_modules`, `.git`, `__pycache__`, `.venv`, `venv`, `.next`, `.cache`, `.DS_Store`, `Thumbs.db`, `desktop.ini`, `dist`, `build`

**符号链接/联接点：** 自动跳过

## 项目结构

```
D:\Openclaw自定义备份工具\
├── openclaw_backup.csproj          # .NET 8 控制台项目
├── Program.cs                      # 程序入口
├── BackupConfig.cs                 # XML 配置加载/保存
├── BackupRunner.cs                 # tar.gz 备份执行核心逻辑
├── ExclusionFilter.cs              # 排除规则管理
├── ServiceManager.cs               # OpenClaw 服务启停管理
├── PrivilegeHelper.cs              # 权限检测与自动提权
├── openclaw_backup.xml             # 默认配置文件（输出到 exe 同目录）
└── bin\Release\net8.0\
    └── openclaw_backup.exe         # 编译产物
```

## 配置文件 openclaw_backup.xml

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
3. 分别创建 `*-config.tar.gz` 和 `*-workspace.tar.gz`
4. 遍历时跳过符号链接和排除规则
5. 如文件被锁定（SQLite）→ `openclaw gateway stop` → 重试 → `openclaw gateway start`
6. 权限不足 → UAC 自动提权
7. 清理过期备份（按 MaxBackups）

### openclaw 命令查找顺序
1. 配置文件缓存路径
2. 系统 PATH 中的 `openclaw` / `openclaw.cmd`
3. `%USERPROFILE%\.openclaw\bin\openclaw`
4. `%LOCALAPPDATA%\Programs\OpenClaw\openclaw`
5. 找不到则提示用户手动输入并缓存

### 权限处理
- 检测到权限不足时，自动触发 UAC 提权
- 使用 `ProcessStartInfo.Verb = "runas"` 重启自身
- 用户拒绝提权 → 提示 "请以管理员身份运行" 并退出

### 压缩包格式
- 配置目录：`openclaw-backup-YYYYMMDD-HHmmss-config.tar.gz`
- 工作空间：`openclaw-backup-YYYYMMDD-HHmmss-workspace.tar.gz`
- tar 内部路径保留绝对路径（如 `C:\Users\...\`）
- 不跟随符号链接
