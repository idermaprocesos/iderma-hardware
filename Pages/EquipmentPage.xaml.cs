using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using HardwareRegistro.Models;
using HardwareRegistro.Services;

namespace HardwareRegistro.Pages;

public sealed partial class EquipmentPage : Page
{
    private bool _loading = true;
    private bool _isNew = true;
    private bool _codeTouched;
    private Guid? _id;
    private DateTimeOffset _createdAt;
    private EquipmentKind _currentKind = EquipmentKind.Pc;
    private string _autoCode = "";

    public EquipmentPage()
    {
        InitializeComponent();
        foreach (var kind in Enum.GetValues<EquipmentKind>())
        {
            KindBox.Items.Add(new ComboBoxItem
            {
                Content = EquipmentCatalog.Name(kind),
                Tag = kind
            });
        }

        foreach (var status in EquipmentCatalog.Statuses)
        {
            StatusBox.Items.Add(status);
        }
    }

    public ObservableCollection<ComponentEntry> Components { get; } = [];

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var args = e.Parameter as EquipmentEditorArgs;
        _loading = true;

        if (args?.EquipmentId is Guid id && InventoryStore.Instance.Find(id) is EquipmentRecord record)
        {
            _isNew = false;
            _id = record.Id;
            _createdAt = record.CreatedAt;
            _currentKind = record.Kind;
            _codeTouched = true;
            EditorTitle.Text = "Editar equipo";
            CodeBox.Text = record.InternalCode;
            BrandBox.Text = record.Brand;
            ModelBox.Text = record.Model;
            SerialBox.Text = record.SerialNumber;
            LocationBox.Text = record.Location;
            AssigneeBox.Text = record.AssignedTo;
            NotesBox.Text = record.Notes;
            StatusBox.Text = string.IsNullOrWhiteSpace(record.Status)
                ? EquipmentCatalog.DefaultStatus
                : record.Status;
            SelectKind(record.Kind);
            Components.Clear();
            foreach (var component in record.Components)
            {
                Components.Add(new ComponentEntry
                {
                    Kind = record.Kind,
                    Name = component.Name,
                    Detail = component.Detail
                });
            }
        }
        else
        {
            _isNew = true;
            _id = null;
            _createdAt = DateTimeOffset.Now;
            _codeTouched = false;
            var kind = args?.Kind ?? EquipmentKind.Pc;
            _currentKind = kind;
            EditorTitle.Text = "Registrar equipo";
            StatusBox.Text = EquipmentCatalog.DefaultStatus;
            SelectKind(kind);
            ApplyPreset(kind);
            ApplyAutoCode(kind);
        }

        ValidationBar.IsOpen = false;
        _loading = false;
    }

    private EquipmentKind SelectedKind =>
        KindBox.SelectedItem is ComboBoxItem { Tag: EquipmentKind kind }
            ? kind
            : _currentKind;

    private void SelectKind(EquipmentKind kind)
    {
        foreach (var item in KindBox.Items)
        {
            if (item is ComboBoxItem { Tag: EquipmentKind value } && value == kind)
            {
                KindBox.SelectedItem = item;
                return;
            }
        }
    }

    private void ApplyPreset(EquipmentKind kind)
    {
        Components.Clear();
        foreach (var preset in EquipmentCatalog.Presets(kind))
        {
            Components.Add(new ComponentEntry
            {
                Kind = kind,
                Name = preset.Name
            });
        }
    }

    private void ApplyAutoCode(EquipmentKind kind)
    {
        _autoCode = EquipmentCatalog.NextCode(kind, InventoryStore.Instance.Items);
        var wasLoading = _loading;
        _loading = true;
        CodeBox.Text = _autoCode;
        _loading = wasLoading;
        _codeTouched = false;
    }

    private bool ComponentsMatchPreset(EquipmentKind kind)
    {
        var preset = EquipmentCatalog.Presets(kind);
        if (Components.Count != preset.Count)
        {
            return false;
        }

        for (var index = 0; index < preset.Count; index++)
        {
            if (!string.Equals(Components[index].Name, preset[index].Name, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(Components[index].Detail))
            {
                return false;
            }
        }

        return true;
    }

    private void CodeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _codeTouched = !string.Equals(CodeBox.Text.Trim(), _autoCode, StringComparison.OrdinalIgnoreCase);
    }

    private async void KindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var next = SelectedKind;
        if (next == _currentKind)
        {
            return;
        }

        if (!ComponentsMatchPreset(_currentKind))
        {
            var dialog = new ContentDialog
            {
                Title = "Cambiar componentes",
                Content = "Este equipo ya tiene componentes definidos. ¿Quieres reemplazarlos por la lista sugerida del nuevo tipo?",
                PrimaryButtonText = "Reemplazar",
                CloseButtonText = "Conservar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                _loading = true;
                SelectKind(_currentKind);
                _loading = false;
                return;
            }
        }

        _currentKind = next;
        ApplyPreset(next);
        if (_isNew && !_codeTouched)
        {
            ApplyAutoCode(next);
        }
    }

    private void AddComponent_Click(object sender, RoutedEventArgs e)
    {
        Components.Add(new ComponentEntry { Kind = _currentKind });
    }

    private void RemoveComponent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ComponentEntry entry })
        {
            Components.Remove(entry);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Leave();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var code = CodeBox.Text.Trim();
        if (code.Length == 0)
        {
            ShowValidation("Escribe el código interno del equipo.");
            CodeBox.Focus(FocusState.Programmatic);
            return;
        }

        if (InventoryStore.Instance.CodeExists(code, _id))
        {
            ShowValidation($"Ya existe un equipo con el código {code}.");
            CodeBox.Focus(FocusState.Programmatic);
            return;
        }

        var status = StatusBox.Text.Trim();
        var record = new EquipmentRecord
        {
            Id = _id ?? Guid.NewGuid(),
            InternalCode = code,
            Kind = _currentKind,
            Brand = BrandBox.Text.Trim(),
            Model = ModelBox.Text.Trim(),
            SerialNumber = SerialBox.Text.Trim(),
            Location = LocationBox.Text.Trim(),
            AssignedTo = AssigneeBox.Text.Trim(),
            Status = status.Length == 0 ? EquipmentCatalog.DefaultStatus : status,
            Notes = NotesBox.Text.Trim(),
            CreatedAt = _createdAt,
            Components = Components
                .Where(component => !string.IsNullOrWhiteSpace(component.Name) || !string.IsNullOrWhiteSpace(component.Detail))
                .Select(component => new EquipmentComponent
                {
                    Name = component.Name.Trim(),
                    Detail = component.Detail.Trim()
                })
                .ToList()
        };

        InventoryStore.Instance.Upsert(record);
        Leave();
    }

    private void ShowValidation(string message)
    {
        ValidationBar.Message = message;
        ValidationBar.IsOpen = true;
    }

    private void Leave()
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
            return;
        }

        Frame.Navigate(typeof(InventoryPage));
    }
}
