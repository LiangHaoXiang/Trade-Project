using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using TradeDashboard.Models;

namespace TradeDashboard.Services;

public class SqliteDataService : IDataService
{
    #region 私有变量

    private readonly string m_ConnectionString;

    #endregion

    #region 构造函数

    public SqliteDataService(IConfigurationService configService)
    {
        var dbPath = configService.GetDatabasePath();
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        m_ConnectionString = $"Data Source={dbPath}";
        InitializeSchema();
    }

    #endregion

    #region 公有接口

    public async Task<IReadOnlyList<DailyBar>> GetDailyBarsAsync(string symbol, string startDate, string endDate)
    {
        var bars = new List<DailyBar>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT symbol, date, open, high, low, close, volume, amount
            FROM daily
            WHERE symbol = @symbol AND date BETWEEN @start AND @end
            ORDER BY date
            """;
        cmd.Parameters.AddWithValue("@symbol", symbol);
        cmd.Parameters.AddWithValue("@start", startDate);
        cmd.Parameters.AddWithValue("@end", endDate);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bars.Add(ReadDailyBar(reader));
        }
        return bars;
    }

    public async Task<IReadOnlyList<DailyBar>> GetLatestDailyBarsAsync(string symbol, int count)
    {
        var bars = new List<DailyBar>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT symbol, date, open, high, low, close, volume, amount
            FROM daily
            WHERE symbol = @symbol
            ORDER BY date DESC
            LIMIT @count
            """;
        cmd.Parameters.AddWithValue("@symbol", symbol);
        cmd.Parameters.AddWithValue("@count", count);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bars.Add(ReadDailyBar(reader));
        }

        bars.Reverse();
        return bars;
    }

    public async Task<IReadOnlyList<DailyBar>> GetDailyBarsBeforeAsync(string symbol, string beforeDate, int count)
    {
        var bars = new List<DailyBar>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT symbol, date, open, high, low, close, volume, amount
            FROM daily
            WHERE symbol = @symbol AND date < @beforeDate
            ORDER BY date DESC
            LIMIT @count
            """;
        cmd.Parameters.AddWithValue("@symbol", symbol);
        cmd.Parameters.AddWithValue("@beforeDate", beforeDate);
        cmd.Parameters.AddWithValue("@count", count);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bars.Add(ReadDailyBar(reader));
        }

        bars.Reverse();
        return bars;
    }

    public async Task<IReadOnlyList<string>> GetAvailableSymbolsAsync()
    {
        var symbols = new List<string>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT symbol FROM daily ORDER BY symbol";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            symbols.Add(reader.GetString(0));
        }
        return symbols;
    }

    public async Task<IReadOnlyList<StockInfo>> GetStockListAsync()
    {
        var stocks = new List<StockInfo>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT symbol, name FROM stock_basic ORDER BY symbol";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stocks.Add(new StockInfo
            {
                Symbol = reader.GetString(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
            });
        }
        return stocks;
    }

    public async Task<int> SaveBacktestResultAsync(BacktestResult result)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        // Insert backtest run
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = """
                INSERT INTO backtest_runs (symbol, strategy_name, start_date, end_date, initial_cash, final_value, total_return_pct, max_drawdown_pct, trade_count, run_at, metrics_json)
                VALUES (@symbol, @strategy, @start, @end, @initial, @final, @returnPct, @maxDD, @tradeCount, @runAt, @metricsJson);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@symbol", result.Symbol);
            cmd.Parameters.AddWithValue("@strategy", result.StrategyName);
            cmd.Parameters.AddWithValue("@start", result.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@end", result.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@initial", result.InitialCash);
            cmd.Parameters.AddWithValue("@final", result.FinalValue);
            cmd.Parameters.AddWithValue("@returnPct", result.TotalReturnPct);
            cmd.Parameters.AddWithValue("@maxDD", result.MaxDrawdownPct);
            cmd.Parameters.AddWithValue("@tradeCount", result.TradeCount);
            cmd.Parameters.AddWithValue("@runAt", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@metricsJson", System.Text.Json.JsonSerializer.Serialize(result.Metrics));

            result.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // Insert trades
        foreach (var trade in result.Trades)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = """
                INSERT INTO trades (backtest_run_id, symbol, direction, price, volume, trade_date, reason)
                VALUES (@runId, @symbol, @direction, @price, @volume, @date, @reason)
                """;
            cmd.Parameters.AddWithValue("@runId", result.Id);
            cmd.Parameters.AddWithValue("@symbol", trade.Symbol);
            cmd.Parameters.AddWithValue("@direction", trade.Direction);
            cmd.Parameters.AddWithValue("@price", trade.Price);
            cmd.Parameters.AddWithValue("@volume", trade.Volume);
            cmd.Parameters.AddWithValue("@date", trade.TradeDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@reason", trade.Reason ?? "");
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert equity curve
        for (int i = 0; i < result.EquityCurve.Count; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = """
                INSERT INTO equity_curve (backtest_run_id, date, equity)
                VALUES (@runId, @date, @equity)
                """;
            cmd.Parameters.AddWithValue("@runId", result.Id);
            cmd.Parameters.AddWithValue("@date", result.EquityCurve[i].Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@equity", result.EquityCurve[i].Equity);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return result.Id;
    }

    public async Task<BacktestResult?> GetLatestBacktestResultAsync()
    {
        var history = await GetBacktestHistoryAsync();
        return history.LastOrDefault();
    }

    public async Task<IReadOnlyList<BacktestResult>> GetBacktestHistoryAsync()
    {
        var results = new List<BacktestResult>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, symbol, strategy_name, start_date, end_date, initial_cash, final_value, total_return_pct, max_drawdown_pct, trade_count, run_at, metrics_json FROM backtest_runs ORDER BY id";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var result = new BacktestResult
            {
                Id = reader.GetInt32(0),
                Symbol = reader.GetString(1),
                StrategyName = reader.GetString(2),
                StartDate = DateTime.Parse(reader.GetString(3)),
                EndDate = DateTime.Parse(reader.GetString(4)),
                InitialCash = reader.GetDouble(5),
                FinalValue = reader.GetDouble(6),
                TotalReturnPct = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                MaxDrawdownPct = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                TradeCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                RunAt = DateTime.Parse(reader.GetString(10)),
            };

            // 反序列化 metrics_json
            if (!reader.IsDBNull(11))
            {
                try
                {
                    var metricsJson = reader.GetString(11);
                    result.Metrics = System.Text.Json.JsonSerializer.Deserialize<BacktestMetrics>(metricsJson) ?? new BacktestMetrics();
                }
                catch (System.Text.Json.JsonException)
                {
                    result.Metrics = new BacktestMetrics();
                }
            }

            results.Add(result);
        }

        // Load trades and equity for the latest result
        foreach (var result in results)
        {
            result.Trades = (List<Trade>)await GetTradesAsync(result.Id);
            result.EquityCurve = (List<EquityPoint>)await GetEquityCurveAsync(result.Id);
        }

        return results;
    }

    public async Task<IReadOnlyList<Trade>> GetTradesAsync(int backtestRunId)
    {
        var trades = new List<Trade>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, backtest_run_id, symbol, direction, price, volume, trade_date, reason
            FROM trades WHERE backtest_run_id = @runId ORDER BY trade_date
            """;
        cmd.Parameters.AddWithValue("@runId", backtestRunId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            trades.Add(new Trade
            {
                Id = reader.GetInt32(0),
                BacktestRunId = reader.GetInt32(1),
                Symbol = reader.GetString(2),
                Direction = reader.GetString(3),
                Price = reader.GetDouble(4),
                Volume = reader.GetInt32(5),
                TradeDate = DateTime.Parse(reader.GetString(6)),
                Reason = reader.IsDBNull(7) ? "" : reader.GetString(7),
            });
        }
        return trades;
    }

    public async Task<IReadOnlyList<Trade>> GetAllTradesAsync(string? symbol = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var trades = new List<Trade>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();

        var sql = "SELECT id, backtest_run_id, symbol, direction, price, volume, trade_date, reason FROM trades WHERE 1=1";
        if (!string.IsNullOrEmpty(symbol))
        {
            sql += " AND symbol = @symbol";
            cmd.Parameters.AddWithValue("@symbol", symbol);
        }
        if (startDate.HasValue)
        {
            sql += " AND trade_date >= @start";
            cmd.Parameters.AddWithValue("@start", startDate.Value.ToString("yyyy-MM-dd"));
        }
        if (endDate.HasValue)
        {
            sql += " AND trade_date <= @end";
            cmd.Parameters.AddWithValue("@end", endDate.Value.ToString("yyyy-MM-dd"));
        }
        sql += " ORDER BY trade_date DESC";
        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            trades.Add(new Trade
            {
                Id = reader.GetInt32(0),
                BacktestRunId = reader.GetInt32(1),
                Symbol = reader.GetString(2),
                Direction = reader.GetString(3),
                Price = reader.GetDouble(4),
                Volume = reader.GetInt32(5),
                TradeDate = DateTime.Parse(reader.GetString(6)),
                Reason = reader.IsDBNull(7) ? "" : reader.GetString(7),
            });
        }
        return trades;
    }

    public async Task<IReadOnlyList<EquityPoint>> GetEquityCurveAsync(int backtestRunId)
    {
        var points = new List<EquityPoint>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT date, equity FROM equity_curve
            WHERE backtest_run_id = @runId ORDER BY date
            """;
        cmd.Parameters.AddWithValue("@runId", backtestRunId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            points.Add(new EquityPoint
            {
                Date = DateTime.Parse(reader.GetString(0)),
                Equity = reader.GetDouble(1),
            });
        }
        return points;
    }

    public async Task<PortfolioSummary> GetPortfolioSummaryAsync()
    {
        var latest = await GetLatestBacktestResultAsync();
        if (latest == null)
        {
            return new PortfolioSummary();
        }

        return new PortfolioSummary
        {
            TotalValue = latest.FinalValue,
            TotalPnl = latest.Pnl,
            PnlPercent = latest.PnlPct,
            TradeCount = latest.TradeCount,
            MaxDrawdown = latest.MaxDrawdownPct,
        };
    }

    public async Task<IReadOnlyList<StockInfo>> GetFavoriteStocksAsync()
    {
        var stocks = new List<StockInfo>();
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.symbol, s.name
            FROM favorite_stocks f
            LEFT JOIN stock_basic s ON f.symbol = s.symbol
            ORDER BY f.sort_order, f.added_at
            """;
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stocks.Add(new StockInfo
            {
                Symbol = reader.GetString(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
            });
        }
        return stocks;
    }

    public async Task AddFavoriteStockAsync(string symbol)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();

        int maxOrder = 0;
        using (var maxCmd = conn.CreateCommand())
        {
            maxCmd.CommandText = "SELECT COALESCE(MAX(sort_order), 0) FROM favorite_stocks";
            maxOrder = Convert.ToInt32(await maxCmd.ExecuteScalarAsync());
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO favorite_stocks (symbol, added_at, sort_order)
            VALUES (@symbol, @addedAt, @sortOrder)
            """;
        cmd.Parameters.AddWithValue("@symbol", symbol);
        cmd.Parameters.AddWithValue("@addedAt", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("@sortOrder", maxOrder + 1);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveFavoriteStockAsync(string symbol)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM favorite_stocks WHERE symbol = @symbol";
        cmd.Parameters.AddWithValue("@symbol", symbol);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsFavoriteStockAsync(string symbol)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM favorite_stocks WHERE symbol = @symbol";
        cmd.Parameters.AddWithValue("@symbol", symbol);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task ReorderFavoriteStocksAsync(IReadOnlyList<string> symbolsInOrder)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();
        try
        {
            for (int i = 0; i < symbolsInOrder.Count; i++)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)transaction;
                cmd.CommandText = "UPDATE favorite_stocks SET sort_order = @order WHERE symbol = @symbol";
                cmd.Parameters.AddWithValue("@order", i + 1);
                cmd.Parameters.AddWithValue("@symbol", symbolsInOrder[i]);
                await cmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    #endregion

    #region Schema

    private void InitializeSchema()
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS daily (
                symbol TEXT,
                date TEXT,
                open REAL,
                high REAL,
                low REAL,
                close REAL,
                volume REAL,
                amount REAL,
                PRIMARY KEY (symbol, date)
            );

            CREATE TABLE IF NOT EXISTS backtest_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                symbol TEXT NOT NULL,
                strategy_name TEXT NOT NULL,
                start_date TEXT NOT NULL,
                end_date TEXT NOT NULL,
                initial_cash REAL NOT NULL,
                final_value REAL NOT NULL,
                total_return_pct REAL,
                max_drawdown_pct REAL,
                trade_count INTEGER,
                run_at TEXT NOT NULL,
                parameters_json TEXT,
                metrics_json TEXT
            );

            CREATE TABLE IF NOT EXISTS trades (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                backtest_run_id INTEGER NOT NULL,
                symbol TEXT NOT NULL,
                direction TEXT NOT NULL,
                price REAL NOT NULL,
                volume INTEGER NOT NULL,
                trade_date TEXT NOT NULL,
                reason TEXT,
                FOREIGN KEY (backtest_run_id) REFERENCES backtest_runs(id)
            );

            CREATE TABLE IF NOT EXISTS equity_curve (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                backtest_run_id INTEGER NOT NULL,
                date TEXT NOT NULL,
                equity REAL NOT NULL,
                FOREIGN KEY (backtest_run_id) REFERENCES backtest_runs(id)
            );

            CREATE TABLE IF NOT EXISTS stock_basic (
                symbol TEXT PRIMARY KEY,
                name TEXT
            );

            CREATE TABLE IF NOT EXISTS favorite_stocks (
                symbol TEXT PRIMARY KEY,
                added_at TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0
            );
            """;
        cmd.ExecuteNonQuery();

        // 迁移：为旧表添加 metrics_json 字段
        try
        {
            using var migrateCmd = conn.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE backtest_runs ADD COLUMN metrics_json TEXT";
            migrateCmd.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // 字段已存在，忽略
        }

        // 迁移：为 favorite_stocks 添加 sort_order 字段
        try
        {
            using var migrateCmd = conn.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE favorite_stocks ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0";
            migrateCmd.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // 字段已存在，忽略
        }
    }

    #endregion

    #region 私有接口

    private static DailyBar ReadDailyBar(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new DailyBar
        {
            Symbol = reader.GetString(0),
            Date = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
            Open = reader.GetDouble(2),
            High = reader.GetDouble(3),
            Low = reader.GetDouble(4),
            Close = reader.GetDouble(5),
            Volume = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
            Amount = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
        };
    }

    #endregion
}
