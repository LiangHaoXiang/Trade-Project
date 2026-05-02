using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class TradeHistoryViewModel : ObservableObject
{
    #region 私有变量

    private readonly IDataService m_DataService;

    #endregion

    #region 构造函数

    public TradeHistoryViewModel(IDataService dataService)
    {
        m_DataService = dataService;
        _ = LoadDataAsync();
    }

    #endregion

    #region 公有接口

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var trades = await m_DataService.GetAllTradesAsync();
        AllTrades = new ObservableCollection<Trade>(trades);
        Symbols = new ObservableCollection<string>(trades.Select(t => t.Symbol).Distinct().OrderBy(s => s));
        ApplyFilter();
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private ObservableCollection<Trade> _allTrades = [];
    [ObservableProperty] private ListCollectionView? _filteredTradesView;
    [ObservableProperty] private ObservableCollection<string> _symbols = [];
    [ObservableProperty] private string? _filterSymbol;
    [ObservableProperty] private DateTime? _filterStartDate;
    [ObservableProperty] private DateTime? _filterEndDate;
    [ObservableProperty] private string? _filterDirection;
    [ObservableProperty] private Trade? _selectedTrade;
    [ObservableProperty] private ObservableCollection<string> _directions = ["全部", "买入", "卖出"];

    [RelayCommand]
    private void ApplyFilter()
    {
        var filtered = AllTrades.AsEnumerable();
        if (!string.IsNullOrEmpty(FilterSymbol))
        {
            filtered = filtered.Where(t => t.Symbol == FilterSymbol);
        }
        if (FilterStartDate.HasValue)
        {
            filtered = filtered.Where(t => t.TradeDate >= FilterStartDate.Value);
        }
        if (FilterEndDate.HasValue)
        {
            filtered = filtered.Where(t => t.TradeDate <= FilterEndDate.Value);
        }
        if (FilterDirection == "买入")
        {
            filtered = filtered.Where(t => t.Direction == "BUY");
        }
        else if (FilterDirection == "卖出")
        {
            filtered = filtered.Where(t => t.Direction == "SELL");
        }

        var view = new ListCollectionView(filtered.ToList());
        FilteredTradesView = view;
    }

    [RelayCommand]
    private void ResetFilter()
    {
        FilterSymbol = null;
        FilterStartDate = null;
        FilterEndDate = null;
        FilterDirection = null;
        ApplyFilter();
    }

    #endregion
}
