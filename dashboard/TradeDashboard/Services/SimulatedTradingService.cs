using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using TradeDashboard.Models;

namespace TradeDashboard.Services;

public class SimulatedTradingService : ITradingService
{
    #region 常量

    private const double k_Commission = 0.0001;
    private const double k_StampTax = 0.001;
    private const double k_MaxPositionPct = 0.2;
    private const int k_MaxHoldings = 5;

    #endregion

    #region 私有变量

    private readonly string m_ConnectionString;
    private readonly Dictionary<string, SimPosition> m_Positions = new();
    private readonly List<SimOrder> m_Orders = new();
    private double m_Cash = 100_000;
    private double m_InitialCash = 100_000;

    #endregion

    #region 构造函数

    public SimulatedTradingService(IConfigurationService configService)
    {
        var dbPath = configService.GetDatabasePath();
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        m_ConnectionString = $"Data Source={dbPath}";
        InitializeSchema();
        LoadState();
    }

    #endregion

    #region 公有接口

    public SimAccount GetAccount()
    {
        var marketValue = m_Positions.Values.Sum(p => p.Volume * p.CurrentPrice);
        var total = m_Cash + marketValue;
        return new SimAccount
        {
            InitialCash = m_InitialCash,
            Cash = m_Cash,
            MarketValue = marketValue,
            TotalAssets = total,
            PositionCount = m_Positions.Count,
            TodayOrderCount = m_Orders.Count(o => o.OrderTime.Date == DateTime.Today),
        };
    }

    public IReadOnlyList<SimPosition> GetPositions()
    {
        return m_Positions.Values.ToList().AsReadOnly();
    }

    public IReadOnlyList<SimOrder> GetOrders()
    {
        return m_Orders.AsReadOnly();
    }

    public IReadOnlyList<SimOrder> GetTodayOrders()
    {
        return m_Orders.Where(o => o.OrderTime.Date == DateTime.Today).ToList().AsReadOnly();
    }

    public SimOrder Buy(string symbol, double price, int volume, string reason = "")
    {
        var order = new SimOrder
        {
            Symbol = symbol,
            Direction = "BUY",
            Price = price,
            Volume = volume,
            OrderTime = DateTime.Now,
            Reason = reason,
        };

        // 风控检查：单笔仓位
        var orderValue = price * volume;
        var totalAssets = m_Cash + m_Positions.Values.Sum(p => p.Volume * p.CurrentPrice);
        if (totalAssets > 0 && orderValue / totalAssets > k_MaxPositionPct)
        {
            order.Status = "rejected";
            order.Reason = $"单笔仓位超限 {orderValue / totalAssets:P1} > {k_MaxPositionPct:P1}";
            m_Orders.Add(order);
            SaveOrder(order);
            return order;
        }

        // 风控检查：最大持仓数
        if (!m_Positions.ContainsKey(symbol) && m_Positions.Count >= k_MaxHoldings)
        {
            order.Status = "rejected";
            order.Reason = $"持仓数 {m_Positions.Count} 已达上限 {k_MaxHoldings}";
            m_Orders.Add(order);
            SaveOrder(order);
            return order;
        }

        // 风控检查：资金不足
        var cost = orderValue * (1 + k_Commission);
        if (cost > m_Cash)
        {
            order.Status = "rejected";
            order.Reason = "资金不足";
            m_Orders.Add(order);
            SaveOrder(order);
            return order;
        }

        // 执行买入
        m_Cash -= cost;

        if (m_Positions.TryGetValue(symbol, out var existing))
        {
            var totalCost = existing.CostPrice * existing.Volume + price * volume;
            existing.Volume += volume;
            existing.CostPrice = totalCost / existing.Volume;
            existing.CurrentPrice = price;
            existing.AvailableVolume = existing.Volume;
        }
        else
        {
            m_Positions[symbol] = new SimPosition
            {
                Symbol = symbol,
                Volume = volume,
                AvailableVolume = volume,
                CostPrice = price,
                CurrentPrice = price,
            };
        }

        order.Status = "filled";
        m_Orders.Add(order);
        SaveOrder(order);
        SavePosition(m_Positions[symbol]);
        SaveAccountState();
        return order;
    }

    public SimOrder Sell(string symbol, double price, int volume, string reason = "")
    {
        var order = new SimOrder
        {
            Symbol = symbol,
            Direction = "SELL",
            Price = price,
            Volume = volume,
            OrderTime = DateTime.Now,
            Reason = reason,
        };

        if (!m_Positions.TryGetValue(symbol, out var pos) || pos.AvailableVolume < volume)
        {
            order.Status = "rejected";
            order.Reason = pos is null ? "无持仓" : $"可用不足 {pos.AvailableVolume} < {volume}";
            m_Orders.Add(order);
            SaveOrder(order);
            return order;
        }

        var revenue = price * volume * (1 - k_Commission - k_StampTax);
        m_Cash += revenue;

        pos.Volume -= volume;
        pos.AvailableVolume -= volume;
        pos.CurrentPrice = price;

        if (pos.Volume <= 0)
        {
            m_Positions.Remove(symbol);
            DeletePosition(symbol);
        }
        else
        {
            SavePosition(pos);
        }

        order.Status = "filled";
        m_Orders.Add(order);
        SaveOrder(order);
        SaveAccountState();
        return order;
    }

    public void UpdatePrices(Dictionary<string, double> prices)
    {
        foreach (var (symbol, price) in prices)
        {
            if (m_Positions.TryGetValue(symbol, out var pos))
            {
                pos.CurrentPrice = price;
                SavePosition(pos);
            }
        }
        SaveAccountState();
    }

    public void Reset()
    {
        m_Positions.Clear();
        m_Orders.Clear();
        m_Cash = m_InitialCash;

        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sim_orders; DELETE FROM sim_positions; DELETE FROM sim_account_state";
        cmd.ExecuteNonQuery();
        SaveAccountState();
    }

    #endregion

    #region 私有接口 - Schema

    private void InitializeSchema()
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sim_account_state (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                initial_cash REAL NOT NULL,
                cash REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sim_positions (
                symbol TEXT PRIMARY KEY,
                name TEXT DEFAULT '',
                volume INTEGER NOT NULL,
                available_volume INTEGER NOT NULL,
                cost_price REAL NOT NULL,
                current_price REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sim_orders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                symbol TEXT NOT NULL,
                direction TEXT NOT NULL,
                price REAL NOT NULL,
                volume INTEGER NOT NULL,
                order_time TEXT NOT NULL,
                status TEXT NOT NULL,
                reason TEXT DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region 私有接口 - 持久化

    private void LoadState()
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();

        // 加载账户
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT initial_cash, cash FROM sim_account_state WHERE id = 1";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                m_InitialCash = reader.GetDouble(0);
                m_Cash = reader.GetDouble(1);
            }
        }

        // 加载持仓
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT symbol, name, volume, available_volume, cost_price, current_price FROM sim_positions";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var pos = new SimPosition
                {
                    Symbol = reader.GetString(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Volume = reader.GetInt32(2),
                    AvailableVolume = reader.GetInt32(3),
                    CostPrice = reader.GetDouble(4),
                    CurrentPrice = reader.GetDouble(5),
                };
                m_Positions[pos.Symbol] = pos;
            }
        }

        // 加载委托
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, symbol, direction, price, volume, order_time, status, reason FROM sim_orders ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                m_Orders.Add(new SimOrder
                {
                    Id = reader.GetInt32(0),
                    Symbol = reader.GetString(1),
                    Direction = reader.GetString(2),
                    Price = reader.GetDouble(3),
                    Volume = reader.GetInt32(4),
                    OrderTime = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    Status = reader.GetString(6),
                    Reason = reader.IsDBNull(7) ? "" : reader.GetString(7),
                });
            }
        }
    }

    private void SaveAccountState()
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO sim_account_state (id, initial_cash, cash) VALUES (1, @initial, @cash)
            """;
        cmd.Parameters.AddWithValue("@initial", m_InitialCash);
        cmd.Parameters.AddWithValue("@cash", m_Cash);
        cmd.ExecuteNonQuery();
    }

    private void SavePosition(SimPosition pos)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO sim_positions (symbol, name, volume, available_volume, cost_price, current_price)
            VALUES (@symbol, @name, @volume, @avail, @cost, @current)
            """;
        cmd.Parameters.AddWithValue("@symbol", pos.Symbol);
        cmd.Parameters.AddWithValue("@name", pos.Name);
        cmd.Parameters.AddWithValue("@volume", pos.Volume);
        cmd.Parameters.AddWithValue("@avail", pos.AvailableVolume);
        cmd.Parameters.AddWithValue("@cost", pos.CostPrice);
        cmd.Parameters.AddWithValue("@current", pos.CurrentPrice);
        cmd.ExecuteNonQuery();
    }

    private void DeletePosition(string symbol)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sim_positions WHERE symbol = @symbol";
        cmd.Parameters.AddWithValue("@symbol", symbol);
        cmd.ExecuteNonQuery();
    }

    private void SaveOrder(SimOrder order)
    {
        using var conn = new SqliteConnection(m_ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sim_orders (symbol, direction, price, volume, order_time, status, reason)
            VALUES (@symbol, @direction, @price, @volume, @time, @status, @reason);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@symbol", order.Symbol);
        cmd.Parameters.AddWithValue("@direction", order.Direction);
        cmd.Parameters.AddWithValue("@price", order.Price);
        cmd.Parameters.AddWithValue("@volume", order.Volume);
        cmd.Parameters.AddWithValue("@time", order.OrderTime.ToString("o"));
        cmd.Parameters.AddWithValue("@status", order.Status);
        cmd.Parameters.AddWithValue("@reason", order.Reason ?? "");
        order.Id = Convert.ToInt32(cmd.ExecuteScalar());
    }

    #endregion
}
