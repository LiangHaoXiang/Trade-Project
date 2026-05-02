using TradeDashboard.Models;

namespace TradeDashboard.Services;

public interface IBacktestService
{
    Task<BacktestResult> RunBacktestAsync(BacktestParameters parameters, IProgress<string>? progress = null, CancellationToken ct = default);
}
