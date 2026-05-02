using System.IO;
using TradeDashboard.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TradeDashboard.Services;

public class YamlConfigurationService : IConfigurationService
{
    #region 私有变量

    private readonly string m_ProjectRoot;
    private readonly string m_ConfigPath;
    private readonly IDeserializer m_Deserializer;
    private readonly ISerializer m_Serializer;

    #endregion

    #region 构造函数

    public YamlConfigurationService()
    {
        // Resolve project root: walk up from assembly location to find config/settings.yaml
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "config", "settings.yaml")))
            {
                m_ProjectRoot = dir;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        m_ProjectRoot ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..");
        m_ProjectRoot = Path.GetFullPath(m_ProjectRoot);

        m_ConfigPath = Path.Combine(m_ProjectRoot, "config", "settings.yaml");

        m_Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        m_Serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    #endregion

    #region 公有接口

    public string GetProjectRootPath() => m_ProjectRoot;

    public string GetDatabasePath()
    {
        var config = Load();
        return Path.Combine(m_ProjectRoot, config.Database.Path);
    }

    public AppConfiguration Load()
    {
        if (!File.Exists(m_ConfigPath))
        {
            return new AppConfiguration();
        }

        var yaml = File.ReadAllText(m_ConfigPath);
        return m_Deserializer.Deserialize<AppConfiguration>(yaml) ?? new AppConfiguration();
    }

    public void Save(AppConfiguration config)
    {
        var dir = Path.GetDirectoryName(m_ConfigPath)!;
        Directory.CreateDirectory(dir);
        var yaml = m_Serializer.Serialize(config);
        File.WriteAllText(m_ConfigPath, yaml);
    }

    #endregion
}
