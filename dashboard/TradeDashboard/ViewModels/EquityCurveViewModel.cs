using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class EquityCurveViewModel : ObservableObject
{
    #region 私有变量

    private readonly IDataService m_DataService;

    #endregion

    #region 公有属性

    public List<EquityPoint> EquityPoints { get; private set; } = [];
    public List<double> DrawdownPoints { get; private set; } = [];

    #endregion

    #region 构造函数

    public EquityCurveViewModel(IDataService dataService)
    {
        m_DataService = dataService;
    }

    #endregion

    #region 公有接口

    [RelayCommand]
    public async Task LoadLatestAsync()
    {
        InteractionLogService.Write("资金曲线", "加载最新");
        IsLoading = true;
        try
        {
            var latest = await m_DataService.GetLatestBacktestResultAsync();
            if (latest == null)
            {
                return;
            }

            var equity = await m_DataService.GetEquityCurveAsync(latest.Id);
            if (equity.Count == 0)
            {
                return;
            }

            EquityPoints = equity.ToList();
            DrawdownPoints = CalculateDrawdown(equity.Select(e => e.Equity).ToList());

            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private bool _isLoading;

    private static List<double> CalculateDrawdown(IReadOnlyList<double> equity)
    {
        var drawdown = new List<double>();
        var peak = equity[0];
        foreach (var v in equity)
        {
            if (v > peak)
            {
                peak = v;
            }
            drawdown.Add((v - peak) / peak * 100);
        }
        return drawdown;
    }

    #endregion

    #region 事件

    public event EventHandler? DataChanged;

    #endregion
}
