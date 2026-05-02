using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class NewsViewModel : ObservableObject, IDisposable
{
    #region 私有变量

    private readonly INewsService m_NewsService;
    private readonly DispatcherTimer m_RefreshTimer;

    #endregion

    #region 构造函数

    public NewsViewModel(INewsService newsService)
    {
        m_NewsService = newsService;

        m_RefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(30),
        };
        m_RefreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    #endregion

    #region 公有接口

    public void Dispose()
    {
        m_RefreshTimer.Stop();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private ObservableCollection<NewsItem> _newsItems = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "等待加载";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private NewsItem? _selectedNewsItem;
    [ObservableProperty] private bool _autoRefresh = true;
    [ObservableProperty] private string _nextRefreshText = "";

    public ObservableCollection<NewsItem> FilteredNewsItems
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return NewsItems;
            }

            var filtered = NewsItems
                .Where(n => n.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                         || n.Summary.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                         || n.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return new ObservableCollection<NewsItem>(filtered);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredNewsItems));
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusText = "正在获取新闻...";

        try
        {
            var items = await m_NewsService.FetchNewsAsync();
            NewsItems = new ObservableCollection<NewsItem>(items);
            OnPropertyChanged(nameof(FilteredNewsItems));
            StatusText = $"已加载 {items.Count} 条新闻";
            UpdateNextRefreshTime();
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenUrl()
    {
        if (SelectedNewsItem is null || !SelectedNewsItem.HasUrl)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedNewsItem.Url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ToggleAutoRefresh()
    {
        if (AutoRefresh)
        {
            m_RefreshTimer.Start();
            UpdateNextRefreshTime();
        }
        else
        {
            m_RefreshTimer.Stop();
            NextRefreshText = "";
        }
    }

    internal void StartAutoRefresh()
    {
        if (AutoRefresh)
        {
            m_RefreshTimer.Start();
            UpdateNextRefreshTime();
        }
    }

    private void UpdateNextRefreshTime()
    {
        if (AutoRefresh && m_RefreshTimer.IsEnabled)
        {
            var next = DateTime.Now.Add(m_RefreshTimer.Interval);
            NextRefreshText = $"下次刷新: {next:HH:mm}";
        }
    }

    #endregion
}
