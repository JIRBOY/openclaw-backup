namespace OpenclawBackup;

public static class ExclusionFilter
{
    /// <summary>
    /// 判断单个文件是否应被排除（使用默认规则）
    /// </summary>
    public static bool IsExcluded(string filePath) =>
        IsExcluded(filePath, null);

    /// <summary>
    /// 判断单个文件是否应被排除（使用配置规则）
    /// </summary>
    public static bool IsExcluded(string filePath, BackupConfig? config)
    {
        var extensions = config?.GetEffectiveExtensions() ?? BackupConfig.BuiltinExtensions;
        var folders = config?.GetEffectiveFolders() ?? BackupConfig.BuiltinFolders;
        var files = config?.GetEffectiveFiles() ?? BackupConfig.BuiltinFiles;

        var excludedExtensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var excludedFolders = new HashSet<string>(folders, StringComparer.OrdinalIgnoreCase);
        var excludedFiles = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

        var fileName = Path.GetFileName(filePath);
        var ext = Path.GetExtension(fileName);
        var nameLower = fileName.ToLowerInvariant();

        // 去掉开头的点号后检查扩展名（如 .log, .tmp）
        var extNoDot = ext.TrimStart('.');
        if (!string.IsNullOrEmpty(extNoDot) && excludedExtensions.Contains(extNoDot))
            return true;

        // 检查 .tmp 变体（如 main.sqlite.tmp-abc123）
        if (nameLower.Contains(".tmp"))
            return true;

        // 检查 .log 变体
        if (nameLower.EndsWith(".log"))
            return true;

        // 检查特定文件名
        if (excludedFiles.Contains(fileName))
            return true;

        // 检查路径中是否包含排除的文件夹（支持通配符 *）
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
        {
            if (MatchesExclusion(part, excludedFolders))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 匹配排除规则，支持简单的通配符 * 匹配
    /// </summary>
    private static bool MatchesExclusion(string name, HashSet<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.Contains('*'))
            {
                // 简单通配符匹配
                if (WildCardMatch(name, pattern))
                    return true;
            }
            else if (patterns.Contains(name))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 简单通配符匹配（仅支持 *）
    /// </summary>
    private static bool WildCardMatch(string text, string pattern)
    {
        var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
        if (pattern.StartsWith('*') && pattern.EndsWith('*'))
        {
            // *xxx* - 包含
            return parts.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        }
        if (pattern.StartsWith('*'))
        {
            // *xxx - 结尾匹配
            return text.EndsWith(parts[0], StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.EndsWith('*'))
        {
            // xxx* - 开头匹配
            return text.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase);
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
    public static List<string> GetFilesToBackup(IEnumerable<string> sourceDirs, BackupConfig? config = null)
    {
        var result = new List<string>();

        foreach (var sourceDir in sourceDirs)
        {
            if (!Directory.Exists(sourceDir)) continue;

            try
            {
                var enumOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = true
                };

                var files = Directory.EnumerateFiles(sourceDir, "*", enumOptions);
                foreach (var file in files)
                {
                    if (!IsExcluded(file, config) && !HasSymlinkInPath(file, sourceDir))
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
