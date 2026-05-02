using System;
using System.Windows;

namespace TradeDashboard.Services;

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error,
}

public static class ToastService
{
    private static Action<string, ToastLevel>? s_CurrentCallback;

    public static event Action<string, ToastLevel>? ToastRequested
    {
        add => s_CurrentCallback += value;
        remove => s_CurrentCallback -= value;
    }

    public static void Show(string message, ToastLevel level = ToastLevel.Info)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            s_CurrentCallback?.Invoke(message, level);
        }
        else
        {
            Application.Current.Dispatcher.BeginInvoke(() => s_CurrentCallback?.Invoke(message, level));
        }
    }
}
