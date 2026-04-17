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

    /// <summary>需要排除的文件扩展名（不含点号）</summary>
    [XmlArrayItem("Extension")]
    public List<string> ExcludedExtensions { get; set; } = [];

    /// <summary>需要排除的文件夹名</summary>
    [XmlArrayItem("Folder")]
    public List<string> ExcludedFolders { get; set; } = [];

    /// <summary>需要排除的特定文件名</summary>
    [XmlArrayItem("File")]
    public List<string> ExcludedFiles { get; set; } = [];

    /// <summary>
    /// 获取最终生效的排除文件夹列表（配置值 + 内置兜底）
    /// </summary>
    public List<string> GetEffectiveExtensions() => ExcludedExtensions;
    public List<string> GetEffectiveFolders() => ExcludedFolders;
    public List<string> GetEffectiveFiles() => ExcludedFiles;

    /// <summary>内置兜底排除 - 配置文件完全为空时使用</summary>
    internal static readonly List<string> BuiltinExtensions =
        ["log", "tmp", "pyc", "pyo", "pid", "bak", "swp", "swo", "cache"];

    internal static readonly List<string> BuiltinFolders =
    [
        "tmp", "temp", "backup", "logs", "browser",
        "node_modules", ".git", "__pycache__", ".venv", "venv",
        ".next", ".cache", "dist", "build",
        "extensions", "agents", "tasks",
        // OpenClaw 特有 - 冗余备份和浏览器缓存
        "skills-backup", "skills-backup-*",
        ".browser-profile", ".browser_data",
        ".Trash", ".trash", ".clawhub",
        // 运行时数据 - 可自动重建
        "flows", "completions", "delivery-queue",
        ".dreams",
        // 包管理器缓存
        ".npm", ".nuget", ".cargo", ".gradle", ".m2"
    ];

    internal static readonly List<string> BuiltinFiles =
    [
        ".DS_Store", "Thumbs.db", "desktop.ini"
    ];

    /// <summary>
    /// 创建包含默认排除规则的完整配置
    /// </summary>
    public static BackupConfig CreateDefault()
    {
        var config = new BackupConfig
        {
            ExcludedExtensions = new List<string>(BuiltinExtensions),
            ExcludedFolders = new List<string>(BuiltinFolders),
            ExcludedFiles = new List<string>(BuiltinFiles)
        };
        return config;
    }

    public static BackupConfig Load(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                BackupConfig? config;
                var serializer = new XmlSerializer(typeof(BackupConfig));
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream))
                {
                    config = (BackupConfig?)serializer.Deserialize(reader);
                }

                if (config == null)
                    throw new InvalidOperationException("配置文件内容为空或格式无效");

                // 配置文件中的排除列表为空时，填充默认值
                if (config.ExcludedExtensions.Count == 0)
                    config.ExcludedExtensions = new List<string>(BuiltinExtensions);
                if (config.ExcludedFolders.Count == 0)
                    config.ExcludedFolders = new List<string>(BuiltinFolders);
                if (config.ExcludedFiles.Count == 0)
                    config.ExcludedFiles = new List<string>(BuiltinFiles);

                Console.WriteLine($"[INFO] 已加载配置文件: {path}");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] 配置文件加载失败: {ex.Message}，使用默认配置");
            }
        }

        var defaultConfig = CreateDefault();
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
