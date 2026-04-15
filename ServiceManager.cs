using System.Diagnostics;

namespace OpenclawBackup;

public static class ServiceManager
{
    private static readonly string[] OpenclawSearchPaths =
    [
        @"%USERPROFILE%\.openclaw\bin\openclaw",
        @"%USERPROFILE%\.openclaw\bin\openclaw.exe",
        @"%LOCALAPPDATA%\Programs\OpenClaw\openclaw",
        @"%LOCALAPPDATA%\Programs\OpenClaw\openclaw.exe"
    ];

    /// <summary>
    /// 获取 openclaw 命令路径（优先使用缓存）
    /// </summary>
    public static string GetOpenclawCommand(BackupConfig config)
    {
        // 1. 使用缓存路径
        if (!string.IsNullOrWhiteSpace(config.OpenclawCommandPath) && File.Exists(config.OpenclawCommandPath))
        {
            return config.OpenclawCommandPath;
        }

        // 2. 系统 PATH
        var pathInEnv = FindInEnvironment("openclaw");
        if (pathInEnv != null)
        {
            Console.WriteLine($"[INFO] 在 PATH 中找到 openclaw: {pathInEnv}");
            config.OpenclawCommandPath = pathInEnv;
            config.Save(GetConfigPath());
            return pathInEnv;
        }

        // 3. 搜索已知路径
        foreach (var rawPath in OpenclawSearchPaths)
        {
            var resolvedPath = Environment.ExpandEnvironmentVariables(rawPath);
            // 尝试直接匹配
            if (File.Exists(resolvedPath))
            {
                Console.WriteLine($"[INFO] 找到 openclaw: {resolvedPath}");
                config.OpenclawCommandPath = resolvedPath;
                config.Save(GetConfigPath());
                return resolvedPath;
            }

            // 尝试加 .exe
            var exePath = resolvedPath.EndsWith(".exe") ? resolvedPath : resolvedPath + ".exe";
            if (File.Exists(exePath))
            {
                Console.WriteLine($"[INFO] 找到 openclaw: {exePath}");
                config.OpenclawCommandPath = exePath;
                config.Save(GetConfigPath());
                return exePath;
            }
        }

        // 4. 提示用户输入
        return PromptUserForPath(config);
    }

    /// <summary>
    /// 停止 OpenClaw 网关服务
    /// </summary>
    public static bool StopGateway(string openclawPath)
    {
        Console.WriteLine("[INFO] 正在停止 OpenClaw 网关服务...");
        return RunGatewayCommand(openclawPath, "stop");
    }

    /// <summary>
    /// 启动 OpenClaw 网关服务
    /// </summary>
    public static bool StartGateway(string openclawPath)
    {
        Console.WriteLine("[INFO] 正在启动 OpenClaw 网关服务...");
        return RunGatewayCommand(openclawPath, "start");
    }

    private static bool RunGatewayCommand(string openclawPath, string action)
    {
        var ext = Path.GetExtension(openclawPath).ToLowerInvariant();
        string fileName;
        string arguments;

        // .cmd/.bat 需要通过 cmd.exe 执行
        if (ext == ".cmd" || ext == ".bat")
        {
            fileName = "cmd.exe";
            arguments = $"/c \"{openclawPath}\" gateway {action}";
        }
        else if (ext == ".ps1")
        {
            fileName = "powershell.exe";
            arguments = $"-ExecutionPolicy Bypass -File \"{openclawPath}\" gateway {action}";
        }
        else
        {
            fileName = openclawPath;
            arguments = $"gateway {action}";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"[INFO] 网关服务 {action} 成功");
                return true;
            }
            else
            {
                Console.WriteLine($"[WARN] 网关服务 {action} 返回码: {process.ExitCode}");
                if (!string.IsNullOrWhiteSpace(error))
                    Console.WriteLine($"[WARN] 错误输出: {error.Trim()}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] 执行网关命令失败: {ex.Message}");
            return false;
        }
    }

    private static string? FindInEnvironment(string fileName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            var trimmed = dir.Trim();

            // 优先查找 .cmd（npm 安装的 Windows 包装脚本）
            var cmdPath = Path.Combine(trimmed, fileName + ".cmd");
            if (File.Exists(cmdPath))
                return cmdPath;

            // 再查 .exe
            var exePath = Path.Combine(trimmed, fileName + ".exe");
            if (File.Exists(exePath))
                return exePath;

            // 最后查无扩展名（Unix 脚本，通常不可直接执行）
            var fullPath = Path.Combine(trimmed, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private static string PromptUserForPath(BackupConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("[WARN] 无法自动找到 openclaw 命令路径。");
        Console.WriteLine("[WARN] 请手动输入 openclaw 命令的完整路径，或输入空跳过:");
        Console.Write("> ");

        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("[WARN] 跳过 openclaw 命令路径配置，服务管理功能将不可用。");
            return "";
        }

        if (File.Exists(input))
        {
            config.OpenclawCommandPath = input;
            config.Save(GetConfigPath());
            Console.WriteLine($"[INFO] 已保存 openclaw 路径: {input}");
            return input;
        }

        Console.WriteLine($"[WARN] 指定的路径不存在: {input}");
        return "";
    }

    private static string GetConfigPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openclaw_backup.xml");
    }
}
