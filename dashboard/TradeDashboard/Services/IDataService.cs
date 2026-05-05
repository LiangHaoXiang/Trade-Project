using TradeDashboard.Models;

namespace TradeDashboard.Services;

public interface IDataService
{
    // Market data
    Task<IReadOnlyList<DailyBar>> GetDailyBarsAsync(string symbol, string startDate, string endDate);
    Task<IReadOnlyList<DailyBar>> GetLatestDailyBarsAsync(string symbol, int count);
    Task<IReadOnlyList<DailyBar>> GetDailyBarsBeforeAsync(string symbol, string beforeDate, int count);
    Task<IReadOnlyList<string>> GetAvailableSymbolsAsync();
    Task<IReadOnlyList<StockInfo>> GetStockListAsync();
    Task<DateTime?> GetLatestTradeDateAsync();

    // Backtest results
    Task<int> SaveBacktestResultAsync(BacktestResult result);
    Task<BacktestResult?> GetLatestBacktestResultAsync();
    Task<IReadOnlyList<BacktestResult>> GetBacktestHistoryAsync();

    // Trades
    Task<IReadOnlyList<Trade>> GetTradesAsync(int backtestRunId);
    Task<IReadOnlyList<Trade>> GetAllTradesAsync(string? symbol = null, DateTime? startDate = null, DateTime? endDate = null);

    // Equity curve
    Task<IReadOnlyList<EquityPoint>> GetEquityCurveAsync(int backtestRunId);

    // Summary
    Task<PortfolioSummary> GetPortfolioSummaryAsync();

    // Favorite stocks
    Task<IReadOnlyList<StockInfo>> GetFavoriteStocksAsync();
    Task AddFavoriteStockAsync(string symbol);
    Task RemoveFavoriteStockAsync(string symbol);
    Task<bool> IsFavoriteStockAsync(string symbol);
    Task ReorderFavoriteStocksAsync(IReadOnlyList<string> symbolsInOrder);
}
