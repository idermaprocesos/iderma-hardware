using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using HardwareRegistro.Services;

namespace HardwareRegistro.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        PathText.Text = InventoryStore.Instance.DataFilePath;
        CloudText.Text = HardwareNube.Estado;
        HardwareNube.EstadoCambiado += OnCloudChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        HardwareNube.EstadoCambiado -= OnCloudChanged;
    }

    private void OnCloudChanged()
    {
        DispatcherQueue.TryEnqueue(() => CloudText.Text = HardwareNube.Estado);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(InventoryStore.Instance.DataFilePath);
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }
}
