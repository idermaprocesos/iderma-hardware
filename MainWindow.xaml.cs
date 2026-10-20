using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using HardwareRegistro.Models;
using HardwareRegistro.Pages;

namespace HardwareRegistro;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 1100;
            presenter.PreferredMinimumHeight = 700;
        }
    }

    public void Maximize()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        });
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavFrame_Navigated(object sender, NavigationEventArgs e)
    {
        AppTitleBar.IsBackButtonVisible = NavFrame.CanGoBack;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
            NavFrame.BackStack.Clear();
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        var parameter = item.Tag switch
        {
            "pc" => EquipmentKind.Pc,
            "laptop" => EquipmentKind.Laptop,
            "monitor" => EquipmentKind.Monitor,
            "phone" => EquipmentKind.Phone,
            "mouse" => EquipmentKind.Mouse,
            "cable" => EquipmentKind.Cable,
            _ => (EquipmentKind?)null
        };

        if (parameter is EquipmentKind kind)
        {
            NavFrame.Navigate(typeof(InventoryPage), kind);
        }
        else
        {
            NavFrame.Navigate(typeof(InventoryPage));
        }

        NavFrame.BackStack.Clear();
    }
}
