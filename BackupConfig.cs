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
