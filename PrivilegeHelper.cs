using System.Diagnostics;
using System.Security.Principal;

namespace OpenclawBackup;

#pragma warning disable CA1416

public static class PrivilegeHelper
{
    /// <summary>
    /// 检查当前进程是否具有管理员权限
    /// </summary>
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 以管理员权限重新启动当前进程
    /// </summary>
    public static void RequestElevation(string[] args)
    {
        Console.WriteLine("[INFO] 检测到权限不足，正在请求管理员权限...");

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            Arguments = string.Join(" ", args.Select(EscapeArgument)),
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
            Console.WriteLine("[INFO] 已启动管理员权限进程，当前进程退出。");
        }
        catch (Exception)
        {
            Console.WriteLine("[ERROR] 用户拒绝了 UAC 提权请求。");
            Console.WriteLine("[ERROR] 备份需要管理员权限，请以管理员身份运行此程序。");
        }

        Environment.Exit(3);
    }

    /// <summary>
    /// 判断异常是否为权限相关错误
    /// </summary>
    public static bool IsAccessDenied(Exception ex)
    {
        return ex is UnauthorizedAccessException
            || ex.Message.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 转义命令行参数
    /// </summary>
    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        if (!arg.Contains(' ') && !arg.Contains('"'))
            return arg;

        return "\"" + arg.Replace("\"", "\"\"") + "\"";
    }
}

#pragma warning restore CA1416
