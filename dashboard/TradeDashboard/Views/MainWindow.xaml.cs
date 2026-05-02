using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TradeDashboard.Services;
using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class MainWindow : Window
{
    #region 私有变量

    private DispatcherTimer? m_ToastTimer;

    #endregion

    #region 构造函数

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        ToastService.ToastRequested += OnToastRequested;
    }

    #endregion

    #region 私有接口 - 快捷键

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Key == Key.F5)
        {
            _ = vm.RefreshCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var tabIndex = e.Key switch
            {
                Key.D1 => 0,
                Key.D2 => 1,
                Key.D3 => 2,
                Key.D4 => 3,
                Key.D5 => 4,
                Key.D6 => 5,
                Key.D7 => 6,
                Key.D8 => 7,
                _ => -1
            };

            if (tabIndex >= 0)
            {
                vm.SelectedTabIndex = tabIndex;
                e.Handled = true;
                return;
            }
        }
    }

    #endregion

    #region 私有接口 - Toast

    private void OnToastRequested(string message, ToastLevel level)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ToastText.Text = message;

            ToastBorder.Background = level switch
            {
                ToastLevel.Success => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x26, 0xa6, 0x9a)),
                ToastLevel.Warning => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xf9, 0xe2, 0xaf)),
                ToastLevel.Error => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xef, 0x53, 0x50)),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x31, 0x32, 0x44)),
            };

            ToastBorder.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var slideIn = new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(200));
            ToastBorder.BeginAnimation(System.Windows.Controls.Border.OpacityProperty, fadeIn);
            ToastTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideIn);

            m_ToastTimer?.Stop();
            m_ToastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            m_ToastTimer.Tick += (_, _) =>
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                var slideOut = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (_, _) => ToastBorder.Visibility = Visibility.Collapsed;
                ToastBorder.BeginAnimation(System.Windows.Controls.Border.OpacityProperty, fadeOut);
                ToastTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideOut);
                m_ToastTimer.Stop();
            };
            m_ToastTimer.Start();
        });
    }

    #endregion
}
