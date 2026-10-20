using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using HardwareRegistro.Models;
using HardwareRegistro.Services;

namespace HardwareRegistro.Pages;

public sealed partial class InventoryPage : Page
{
    private EquipmentKind? _filter;

    public InventoryPage()
    {
        InitializeComponent();
    }

    public ObservableCollection<EquipmentItem> Equipments { get; } = [];

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _filter = e.Parameter is EquipmentKind kind ? kind : null;
        PageTitle.Text = _filter is EquipmentKind selected
            ? EquipmentCatalog.PluralName(selected)
            : "Todos los equipos";

        var error = InventoryStore.Instance.LoadError;
        LoadErrorBar.IsOpen = error is not null;
        LoadErrorBar.Message = error is null
            ? ""
            : "El archivo de inventario no se pudo leer. " + error;

        InventoryStore.Instance.Changed += OnStoreChanged;
        Reload();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        InventoryStore.Instance.Changed -= OnStoreChanged;
    }

    private void OnStoreChanged()
    {
        DispatcherQueue.TryEnqueue(Reload);
    }

    private void Reload()
    {
        var query = SearchBox.Text.Trim();
        var items = InventoryStore.Instance.Items
            .Where(item => _filter is null || item.Kind == _filter)
            .Where(item => Matches(item, query))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new EquipmentItem(item))
            .ToList();

        Equipments.Clear();
        foreach (var item in items)
        {
            Equipments.Add(item);
        }

        var hasItems = items.Count > 0;
        EquipmentList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        CountText.Text = items.Count == 1 ? "1 equipo" : $"{items.Count} equipos";

        if (hasItems)
        {
            return;
        }

        if (query.Length > 0)
        {
            EmptyTitle.Text = "Sin resultados";
            EmptyMessage.Text = "No hay equipos que coincidan con la búsqueda.";
        }
        else
        {
            EmptyTitle.Text = "No hay equipos registrados";
            EmptyMessage.Text = _filter is EquipmentKind selected
                ? $"Todavía no hay {EquipmentCatalog.PluralName(selected)} en el inventario."
                : "Registra un equipo para empezar el inventario de la oficina.";
        }
    }

    private static bool Matches(EquipmentRecord record, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        var fields = new[]
        {
            record.InternalCode,
            record.Brand,
            record.Model,
            record.SerialNumber,
            record.Location,
            record.AssignedTo,
            record.Status,
            record.Notes,
            EquipmentCatalog.Name(record.Kind)
        };

        if (fields.Any(field => field.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return record.Components.Any(component =>
            component.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || component.Detail.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            Reload();
        }
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(EquipmentPage), new EquipmentEditorArgs { Kind = _filter });
    }

    private void NewAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Register_Click(sender, new RoutedEventArgs());
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EquipmentItem item })
        {
            Frame.Navigate(typeof(EquipmentPage), new EquipmentEditorArgs { EquipmentId = item.Record.Id });
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: EquipmentItem item })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Eliminar equipo",
            Content = $"Se eliminará {item.Code} ({item.KindName}). Esta acción no se puede deshacer.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        InventoryStore.Instance.Delete(item.Record.Id);
        Reload();
    }
}
