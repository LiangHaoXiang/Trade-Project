using System;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class ConfigurationViewModel : ObservableObject
{
    #region 私有变量

    private readonly IConfigurationService m_ConfigService;

    #endregion

    #region 构造函数

    public ConfigurationViewModel(IConfigurationService configService)
    {
        m_ConfigService = configService;
        _ = LoadAsync();
    }

    #endregion

    #region 公有接口

    [RelayCommand]
    public async Task LoadAsync()
    {
        Config = m_ConfigService.Load();
        IsLoaded = true;
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private AppConfiguration _config = new();
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private string _saveMessage = "";

    [RelayCommand]
    private void Save()
    {
        InteractionLogService.Write("配置", "保存配置");
        try
        {
            m_ConfigService.Save(Config);
            SaveMessage = "Configuration saved successfully";
        }
        catch (Exception ex)
        {
            SaveMessage = $"Error saving: {ex.Message}";
            InteractionLogService.Write("配置", $"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Reload()
    {
        InteractionLogService.Write("配置", "重载配置");
        Config = m_ConfigService.Load();
        SaveMessage = "Configuration reloaded";
    }

    #endregion
}
