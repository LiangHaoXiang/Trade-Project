using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class MarketDataViewModel : ObservableObject
{
    #region 私有变量

    private readonly IDataService m_DataService;
    private List<StockInfo> m_AllStocks = [];

    #endregion

    #region 公有接口

    public async Task InitializeAsync()
    {
        await LoadStockListAsync();
        await LoadFavoritesAsync();
    }

    public async Task LoadMoreHistoryAsync()
    {
        if (IsLoadingMore || !HasMoreData || DataGridData.Count == 0)
        {
            return;
        }

        var symbol = DataGridData[0].Symbol;
        var beforeDate = DataGridData[0].Date.ToString("yyyy-MM-dd");

        IsLoadingMore = true;
        try
        {
            var olderBars = await m_DataService.GetDailyBarsBeforeAsync(symbol, beforeDate, 500);
            if (olderBars.Count == 0)
            {
                HasMoreData = false;
                return;
            }

            for (int i = 0; i < olderBars.Count; i++)
            {
                DataGridData.Insert(i, olderBars[i]);
            }

            if (olderBars.Count < 500)
            {
                HasMoreData = false;
            }

            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private ObservableCollection<StockInfo> _filteredStocks = [];
    [ObservableProperty] private StockInfo? _selectedStock;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ObservableCollection<DailyBar> _dataGridData = [];
    [ObservableProperty] private bool _showMA5 = true;
    [ObservableProperty] private bool _showMA20 = true;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasMoreData = true;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private ObservableCollection<StockInfo> _favoriteStocks = [];
    [ObservableProperty] private bool _isCurrentStockFavorite;
    [ObservableProperty] private int _leftTabSelectedIndex;

    #endregion

    #region 构造函数

    public MarketDataViewModel(IDataService dataService)
    {
        m_DataService = dataService;
    }

    #endregion

    #region 私有接口

    private async Task LoadStockListAsync()
    {
        var stocks = await m_DataService.GetStockListAsync();
        m_AllStocks = stocks.ToList();
        ApplyFilter();
        if (FilteredStocks.Count > 0)
        {
            SelectedStock = FilteredStocks[0];
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedStockChanged(StockInfo? value)
    {
        if (value is not null)
        {
            IsCurrentStockFavorite = FavoriteStocks.Any(f => f.Symbol == value.Symbol);
            _ = LoadDataForStockAsync(value.Symbol);
        }
    }

    [RelayCommand]
    private async Task AddToFavoritesAsync()
    {
        if (SelectedStock is null) return;
        await m_DataService.AddFavoriteStockAsync(SelectedStock.Symbol);
        await LoadFavoritesAsync();
        IsCurrentStockFavorite = true;
    }

    [RelayCommand]
    private async Task RemoveFromFavoritesAsync(string symbol)
    {
        await m_DataService.RemoveFavoriteStockAsync(symbol);
        await LoadFavoritesAsync();
        if (SelectedStock?.Symbol == symbol)
        {
            IsCurrentStockFavorite = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(string symbol)
    {
        if (FavoriteStocks.Any(f => f.Symbol == symbol))
        {
            await m_DataService.RemoveFavoriteStockAsync(symbol);
        }
        else
        {
            await m_DataService.AddFavoriteStockAsync(symbol);
        }
        await LoadFavoritesAsync();
        if (SelectedStock is not null)
        {
            IsCurrentStockFavorite = FavoriteStocks.Any(f => f.Symbol == SelectedStock.Symbol);
        }
    }

    public bool IsSymbolFavorite(string symbol)
    {
        return FavoriteStocks.Any(f => f.Symbol == symbol);
    }

    [RelayCommand]
    private void SelectFavoriteStock(StockInfo stock)
    {
        SelectedStock = stock;
    }

    public async Task MoveFavoriteAsync(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= FavoriteStocks.Count || toIndex < 0 || toIndex >= FavoriteStocks.Count)
        {
            return;
        }

        var item = FavoriteStocks[fromIndex];
        FavoriteStocks.RemoveAt(fromIndex);
        FavoriteStocks.Insert(toIndex, item);

        var orderedSymbols = FavoriteStocks.Select(f => f.Symbol).ToList();
        await m_DataService.ReorderFavoriteStocksAsync(orderedSymbols);
    }

    private async Task LoadFavoritesAsync()
    {
        var favs = await m_DataService.GetFavoriteStocksAsync();
        FavoriteStocks = new ObservableCollection<StockInfo>(favs);
        if (SelectedStock is not null)
        {
            IsCurrentStockFavorite = FavoriteStocks.Any(f => f.Symbol == SelectedStock.Symbol);
        }
    }

    partial void OnShowMA5Changed(bool value) => DataChanged?.Invoke(this, EventArgs.Empty);
    partial void OnShowMA20Changed(bool value) => DataChanged?.Invoke(this, EventArgs.Empty);

    private void ApplyFilter()
    {
        var query = m_AllStocks.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            query = query.Where(s =>
                s.Symbol.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }
        FilteredStocks = new ObservableCollection<StockInfo>(query.ToList());
    }

    private async Task LoadDataForStockAsync(string symbol)
    {
        IsLoading = true;
        try
        {
            var bars = await m_DataService.GetLatestDailyBarsAsync(symbol, 500);

            if (bars.Count == 0)
            {
                DataGridData = new ObservableCollection<DailyBar>();
                HasMoreData = false;
                DataChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            DataGridData = new ObservableCollection<DailyBar>(bars);
            HasMoreData = bars.Count >= 500;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region 事件

    public event EventHandler? DataChanged;

    #endregion
}
