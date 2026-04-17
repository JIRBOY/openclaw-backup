using System.Xml.Serialization;

namespace OpenclawBackup;

[XmlRoot("OpenclawBackup")]
public class BackupConfig
{
    /// <summary>配置目录路径，留空则自动检测</summary>
    public string ConfigDirectory { get; set; } = "";

    /// <summary>工作空间目录路径，留空则自动检测</summary>
    public string WorkspaceDirectory { get; set; } = @"D:\Personal\OpenClaw";

    /// <summary>openclaw 命令路径，自动查找后缓存</summary>
    public string OpenclawCommandPath { get; set; } = "";

    /// <summary>输出目录</summary>
    public string OutputDirectory { get; set; } = @"D:\Personal\Archive";

    /// <summary>文件名前缀</summary>
    public string FileNamePrefix { get; set; } = "openclaw-backup";

    /// <summary>保留最近 N 个备份，0 表示不限制</summary>
    public int MaxBackups { get; set; } = 30;

    /// <summary>需要排除的文件扩展名（不含点号），留空则使用默认值</summary>
    [XmlArrayItem("Extension")]
    public List<string> ExcludedExtensions { get; set; } = [];

    /// <summary>需要排除的文件夹名，留空则使用默认值</summary>
    [XmlArrayItem("Folder")]
    public List<string> ExcludedFolders { get; set; } = [];

    /// <summary>需要排除的特定文件名，留空则使用默认值</summary>
    [XmlArrayItem("File")]
    public List<string> ExcludedFiles { get; set; } = [];

    /// <summary>默认排除的文件扩展名</summary>
    public static List<string> DefaultExcludedExtensions =>
        ["log", "tmp", "pyc", "pyo", "pid", "bak", "swp", "swo", "cache"];

    /// <summary>默认排除的文件夹名</summary>
    public static List<string> DefaultExcludedFolders =>
    [
        "tmp", "temp", "backup", "logs", "browser",
        "node_modules", ".git", "__pycache__", ".venv", "venv",
        ".next", ".cache", "dist", "build",
        "extensions", "agents", "tasks",
        // OpenClaw 特有 - 冗余备份和浏览器缓存
        "skills-backup", "skills-backup-*",
        ".browser-profile", ".browser_data",
        ".Trash", ".trash", ".clawhub",
        // 包管理器缓存
        ".npm", ".nuget", ".cargo", ".gradle", ".m2"
    ];

    /// <summary>默认排除的特定文件名</summary>
    public static List<string> DefaultExcludedFiles =>
    [
        ".DS_Store", "Thumbs.db", "desktop.ini"
    ];

    /// <summary>
    /// 获取最终生效的排除规则（配置值覆盖默认值）
    /// </summary>
    public List<string> GetEffectiveExtensions() =>
        ExcludedExtensions.Count > 0 ? ExcludedExtensions : DefaultExcludedExtensions;

    public List<string> GetEffectiveFolders() =>
        ExcludedFolders.Count > 0 ? ExcludedFolders : DefaultExcludedFolders;

    public List<string> GetEffectiveFiles() =>
        ExcludedFiles.Count > 0 ? ExcludedFiles : DefaultExcludedFiles;

    public static BackupConfig Load(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                var serializer = new XmlSerializer(typeof(BackupConfig));
                using var reader = new StreamReader(path);
                var config = (BackupConfig)serializer.Deserialize(reader)!;
                Console.WriteLine($"[INFO] 已加载配置文件: {path}");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] 配置文件加载失败: {ex.Message}，使用默认配置");
            }
        }

        var defaultConfig = new BackupConfig();
        defaultConfig.Save(path);
        Console.WriteLine($"[INFO] 已创建默认配置文件: {path}");
        return defaultConfig;
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var serializer = new XmlSerializer(typeof(BackupConfig));
        using var writer = new StreamWriter(path);
        serializer.Serialize(writer, this);
    }
}
