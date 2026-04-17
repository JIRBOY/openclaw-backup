using OpenclawBackup;

// 程序入口点
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  OpenClaw 自动备份工具 v1.0");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // 解析命令行参数
        var configPath = ParseConfigPath(args);
        var dryRun = args.Contains("--dry-run");

        if (args.Contains("--help") || args.Contains("-h"))
        {
            ShowHelp();
            return 0;
        }

        // 加载配置
        var config = BackupConfig.Load(configPath);

        // 检测和解析源目录
        var sourceDirs = ResolveSourceDirs(config);
        if (sourceDirs.Count == 0)
        {
            Console.WriteLine("[ERROR] 找不到任何可备份的源目录。");
            return 1;
        }

        // 确保输出目录存在
        if (!Directory.Exists(config.OutputDirectory))
        {
            Directory.CreateDirectory(config.OutputDirectory);
            Console.WriteLine($"[INFO] 已创建输出目录: {config.OutputDirectory}");
        }

        // Dry-run 模式
        if (dryRun)
        {
            Console.WriteLine("=== 模拟运行模式 ===");
            Console.WriteLine();
            BackupRunner.Run(sourceDirs, config.OutputDirectory,
                config.FileNamePrefix, DateTime.Now.ToString("yyyyMMdd-HHmmss"), dryRun: true, config: config);
            return 0;
        }

        // 执行备份（带重试逻辑）
        var dateTimeSuffix = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var result = ExecuteBackupWithRetry(sourceDirs, config, dateTimeSuffix);

        if (!result.Success)
        {
            Console.WriteLine("[ERROR] 部分备份失败。");
            return 1;
        }

        // 清理过期备份
        BackupRunner.CleanupOldBackups(config.OutputDirectory, config.FileNamePrefix, config.MaxBackups);

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("  备份任务完成");
        Console.WriteLine("========================================");
        return 0;
    }

    /// <summary>
    /// 执行备份，带重试逻辑（文件锁定时自动 stop 服务重试）
    /// </summary>
    static BackupResult ExecuteBackupWithRetry(
        List<string> sourceDirs,
        BackupConfig config,
        string dateTimeSuffix)
    {
        // 第一次尝试
        var result = BackupRunner.Run(sourceDirs, config.OutputDirectory,
            config.FileNamePrefix, dateTimeSuffix, config: config);

        if (result.Success)
            return result;

        // 备份失败，尝试通过停止服务解决
        Console.WriteLine();
        Console.WriteLine("[WARN] 首次备份失败，尝试停止 OpenClaw 服务后重试...");

        var openclawPath = ServiceManager.GetOpenclawCommand(config);
        if (string.IsNullOrEmpty(openclawPath))
        {
            Console.WriteLine("[ERROR] 无法获取 openclaw 命令路径，无法停止服务。");
            return result;
        }

        bool serviceWasStopped = false;
        try
        {
            serviceWasStopped = ServiceManager.StopGateway(openclawPath);
            if (!serviceWasStopped)
            {
                Console.WriteLine("[WARN] 停止服务失败，无法重试。");
                return result;
            }

            // 等待服务完全停止
            Thread.Sleep(2000);

            // 重试备份
            Console.WriteLine("[INFO] 重试备份...");
            result = BackupRunner.Run(sourceDirs, config.OutputDirectory,
                config.FileNamePrefix, dateTimeSuffix, config: config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] 重试备份时出错: {ex.Message}");
        }
        finally
        {
            // 无论成功失败，都恢复服务
            if (serviceWasStopped)
            {
                Console.WriteLine();
                ServiceManager.StartGateway(openclawPath);
            }
        }

        return result;
    }

    /// <summary>
    /// 解析源目录，自动检测缺失路径
    /// </summary>
    static List<string> ResolveSourceDirs(BackupConfig config)
    {
        bool configChanged = false;
        var sourceDirs = new List<string>();

        // 配置目录
        var configDir = ResolveDirectory(
            config.ConfigDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw"),
            "配置目录");

        if (configDir != null)
        {
            sourceDirs.Add(configDir);
            if (config.ConfigDirectory != configDir)
            {
                config.ConfigDirectory = configDir;
                configChanged = true;
            }
        }

        // 工作空间目录
        var workspaceDir = ResolveDirectory(
            config.WorkspaceDirectory,
            @"D:\Personal\OpenClaw",
            "工作空间目录");

        if (workspaceDir != null)
        {
            sourceDirs.Add(workspaceDir);
            if (config.WorkspaceDirectory != workspaceDir)
            {
                config.WorkspaceDirectory = workspaceDir;
                configChanged = true;
            }
        }

        // 只在配置有变更时才保存
        if (configChanged)
        {
            config.Save(GetConfigPath());
        }

        return sourceDirs;
    }

    /// <summary>
    /// 解析单个目录：优先配置路径，其次默认路径，最后尝试搜索
    /// </summary>
    static string? ResolveDirectory(string configValue, string defaultPath, string dirName)
    {
        // 1. 配置文件指定
        if (!string.IsNullOrWhiteSpace(configValue) && Directory.Exists(configValue))
        {
            Console.WriteLine($"[INFO] {dirName}: {configValue}");
            return configValue;
        }

        // 2. 默认路径
        if (Directory.Exists(defaultPath))
        {
            Console.WriteLine($"[INFO] {dirName} (默认): {defaultPath}");
            return defaultPath;
        }

        // 3. 尝试搜索
        Console.WriteLine($"[WARN] {dirName} 默认路径不存在: {defaultPath}");

        // 尝试搜索常见位置
        var searchPaths = GetSearchPathsForDir(dirName);
        foreach (var p in searchPaths)
        {
            if (Directory.Exists(p))
            {
                Console.WriteLine($"[INFO] {dirName} 已找到: {p}");
                return p;
            }
        }

        Console.WriteLine($"[WARN] 未找到{dirName}，跳过该目录。");
        return null;
    }

    /// <summary>
    /// 获取目录的搜索路径列表
    /// </summary>
    static List<string> GetSearchPathsForDir(string dirName)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return dirName switch
        {
            "配置目录" =>
            [
                Path.Combine(userProfile, ".openclaw"),
                Path.Combine(localAppData, "OpenClaw", "config"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenClaw")
            ],
            "工作空间目录" =>
            [
                @"D:\Personal\OpenClaw",
                Path.Combine(userProfile, "OpenClaw"),
                Path.Combine(localAppData, "OpenClaw", "workspace")
            ],
            _ => []
        };
    }

    /// <summary>
    /// 解析配置文件路径
    /// </summary>
    static string ParseConfigPath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config")
                return args[i + 1];
        }
        return GetConfigPath();
    }

    static string GetConfigPath()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataDir, "OpenClaw_Backup");
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, "openclaw_backup.xml");
    }

    static void ShowHelp()
    {
        Console.WriteLine("用法: openclaw_backup.exe [选项]");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  --config <path>  指定配置文件路径");
        Console.WriteLine("                   (默认: exe 同目录下的 openclaw_backup.xml)");
        Console.WriteLine("  --dry-run        模拟运行，列出将要备份的文件，不实际执行");
        Console.WriteLine("  --help, -h       显示此帮助信息");
        Console.WriteLine();
        Console.WriteLine("示例:");
        Console.WriteLine("  openclaw_backup.exe");
        Console.WriteLine("  openclaw_backup.exe --dry-run");
        Console.WriteLine("  openclaw_backup.exe --config D:\\config\\openclaw_backup.xml");
    }
}
