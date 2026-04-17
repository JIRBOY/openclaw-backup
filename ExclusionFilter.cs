namespace OpenclawBackup;

public static class ExclusionFilter
{
    /// <summary>需要排除的文件扩展名</summary>
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".tmp", ".pyc", ".pyo", ".pid"
    };

    /// <summary>需要排除的文件夹名</summary>
    private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "tmp", "temp", "backup", "logs", "browser",
        "node_modules", ".git", "__pycache__", ".venv", "venv",
        ".next", ".cache", "dist", "build",
        "extensions", "agents", "tasks"
    };

    /// <summary>需要排除的特定文件名</summary>
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store", "Thumbs.db", "desktop.ini"
    };

    /// <summary>
    /// 判断单个文件是否应被排除
    /// </summary>
    public static bool IsExcluded(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var ext = Path.GetExtension(fileName);
        var nameLower = fileName.ToLowerInvariant();

        // 检查扩展名
        if (ExcludedExtensions.Contains(ext))
            return true;

        // 检查 .tmp 临时文件变体（如 main.sqlite.tmp-abc123）
        if (nameLower.Contains(".tmp"))
            return true;

        // 检查 .log 变体
        if (nameLower.EndsWith(".log"))
            return true;

        // 检查特定文件名
        if (ExcludedFiles.Contains(fileName))
            return true;

        // 检查路径中是否包含排除的文件夹
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
        {
            if (ExcludedFolders.Contains(part))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 判断路径是否为符号链接
    /// </summary>
    public static bool IsSymlink(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取应备份的文件列表（用于 dry-run）
    /// </summary>
    public static List<string> GetFilesToBackup(IEnumerable<string> sourceDirs)
    {
        var result = new List<string>();

        foreach (var sourceDir in sourceDirs)
        {
            if (!Directory.Exists(sourceDir)) continue;

            try
            {
                // 跳过符号链接
                var enumOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = true
                };

                var files = Directory.EnumerateFiles(sourceDir, "*", enumOptions);
                foreach (var file in files)
                {
                    if (!IsExcluded(file) && !HasSymlinkInPath(file, sourceDir))
                        result.Add(file);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[WARN] 无法访问目录: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// 检查路径中是否包含符号链接目录
    /// </summary>
    private static bool HasSymlinkInPath(string filePath, string sourceDir)
    {
        var current = Path.GetFullPath(filePath);
        var root = Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar);

        while (current.Length > root.Length)
        {
            var dir = Path.GetDirectoryName(current);
            if (dir == null || dir == current) break;

            try
            {
                var attrs = File.GetAttributes(current);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                    return true;
            }
            catch
            {
                return true;
            }

            current = dir!;
        }

        return false;
    }
}
