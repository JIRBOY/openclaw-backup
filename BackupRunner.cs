using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

namespace OpenclawBackup;

/// <summary>
/// 单个目录的备份结果
/// </summary>
public record SingleBackupResult(string SourceDir, bool Success, long FileSize);

/// <summary>
/// 整体备份结果
/// </summary>
public record BackupResult(bool Success, string OutputFile, long FileSize, int FileCount);

public static class BackupRunner
{
    /// <summary>
    /// 执行备份操作 - 所有源目录合并到单个 tar.gz
    /// </summary>
    public static BackupResult Run(
        List<string> sourceDirs,
        string outputDir,
        string fileNamePrefix,
        string dateTimeSuffix,
        bool dryRun = false,
        BackupConfig? config = null)
    {
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // 过滤有效目录
        var validDirs = sourceDirs.Where(Directory.Exists).ToList();
        foreach (var dir in sourceDirs.Where(d => !Directory.Exists(d)))
            Console.WriteLine($"[WARN] 源目录不存在，跳过: {dir}");

        if (validDirs.Count == 0)
        {
            Console.WriteLine("[ERROR] 没有有效的源目录。");
            return new BackupResult(false, "", 0, 0);
        }

        var outputFile = Path.Combine(outputDir, $"{fileNamePrefix}-{dateTimeSuffix}.tar.gz");
        return CreateTarGz(validDirs, outputFile, dryRun, config);
    }

    private static string GetDirLabel(string sourceDir)
    {
        var normalized = sourceDir.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains(".openclaw"))
            return "config";
        if (normalized.Contains("openclaw"))
            return "workspace";
        var last = Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(last) ? "unknown" : last;
    }

    private static BackupResult CreateTarGz(
        List<string> sourceDirs,
        string outputFile,
        bool dryRun,
        BackupConfig? config = null)
    {
        // Collect all files from all source directories
        var allFiles = new List<string>();
        long totalBytes = 0;

        foreach (var sourceDir in sourceDirs)
        {
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true
            };

            foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", enumOptions))
            {
                if (ExclusionFilter.IsExcluded(filePath, config))
                    continue;

                if (HasSymlinkInPath(filePath, sourceDir))
                    continue;

                allFiles.Add(filePath);
                totalBytes += new FileInfo(filePath).Length;
            }
        }

        if (dryRun)
        {
            Console.WriteLine($"[DRY-RUN] 将备份 {allFiles.Count} 个文件:");
            foreach (var f in allFiles.Take(50))
                Console.WriteLine($"  {f}");
            if (allFiles.Count > 50)
                Console.WriteLine($"  ... 还有 {allFiles.Count - 50} 个文件");
            Console.WriteLine($"[DRY-RUN] 输出文件: {outputFile}");
            Console.WriteLine();
            return new BackupResult(true, outputFile, 0, allFiles.Count);
        }

        Console.WriteLine($"[INFO] 正在创建 tar.gz 备份: {outputFile}");

        int fileCount = 0;
        long writtenBytes = 0;

        try
        {
            using var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
            using var gzip = new GZipStream(fs, CompressionLevel.Optimal);
            using var writer = new TarWriter(gzip, leaveOpen: true);

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    writer.WriteEntry(filePath, filePath);
                    fileCount++;
                    writtenBytes += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] 跳过文件 {filePath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] 创建备份失败: {ex.Message}");
            if (File.Exists(outputFile))
                File.Delete(outputFile);
            return new BackupResult(false, outputFile, 0, 0);
        }

        var resultInfo = new FileInfo(outputFile);
        Console.WriteLine($"[INFO] 备份完成: {outputFile} ({FormatSize(resultInfo.Length)}, {fileCount} 个文件, 原始 {FormatSize(writtenBytes)})");
        return new BackupResult(true, outputFile, resultInfo.Length, fileCount);
    }

    private static string NormalizeTarPath(string filePath)
    {
        var absPath = Path.GetFullPath(filePath);
        if (absPath.Length >= 2 && absPath[1] == ':')
            absPath = absPath[0] + "_" + absPath.Substring(2);
        return absPath.Replace('\\', '/');
    }

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

        try
        {
            var rootAttrs = File.GetAttributes(sourceDir);
            return (rootAttrs & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    public static void CleanupOldBackups(string outputDir, string fileNamePrefix, int maxBackups)
    {
        if (maxBackups <= 0 || !Directory.Exists(outputDir))
            return;

        var pattern = $"{fileNamePrefix}-*.tar.gz";
        var backups = Directory.GetFiles(outputDir, pattern)
            .OrderByDescending(f => f)
            .ToList();

        if (backups.Count <= maxBackups)
        {
            Console.WriteLine($"[INFO] 当前 {backups.Count} 个备份，未超过限制 {maxBackups}");
            return;
        }

        var toDelete = backups.Skip(maxBackups).ToList();
        foreach (var file in toDelete)
        {
            try
            {
                File.Delete(file);
                Console.WriteLine($"[INFO] 已删除过期备份: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] 删除备份失败: {Path.GetFileName(file)} - {ex.Message}");
            }
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
