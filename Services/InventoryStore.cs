using System.Text.Json;
using Windows.Storage;
using HardwareRegistro.Models;

namespace HardwareRegistro.Services;

public sealed class InventoryStore
{
    public static InventoryStore Instance { get; } = new();

    private readonly object _gate = new();
    private readonly List<EquipmentRecord> _items = [];
    private readonly string _path;

    public event Action? Changed;

    private InventoryStore()
    {
        _path = ResolvePath();
        Load();
    }

    public string DataFilePath => _path;
    public string? LoadError { get; private set; }

    public IReadOnlyList<EquipmentRecord> Items
    {
        get
        {
            lock (_gate)
            {
                return _items.ToList();
            }
        }
    }

    public EquipmentRecord? Find(Guid id)
    {
        lock (_gate)
        {
            return _items.FirstOrDefault(item => item.Id == id);
        }
    }

    public bool CodeExists(string code, Guid? exceptId)
    {
        var normalized = code.Trim();
        lock (_gate)
        {
            return _items.Any(item =>
                item.Id != exceptId
                && string.Equals(item.InternalCode.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Upsert(EquipmentRecord record)
    {
        record.UpdatedAt = DateTimeOffset.Now;
        lock (_gate)
        {
            SaveLocked(record, keepCreated: true);
            Persist();
        }

        Changed?.Invoke();
        HardwareNube.Publicar(record);
    }

    public void Delete(Guid id)
    {
        var removed = false;
        lock (_gate)
        {
            removed = _items.RemoveAll(item => item.Id == id) > 0;
            if (removed)
            {
                Persist();
            }
        }

        if (!removed)
        {
            return;
        }

        Changed?.Invoke();
        HardwareNube.PublicarBaja(id, DateTimeOffset.UtcNow);
    }

    public void AplicarDesdeNube(IReadOnlyList<EquipmentRecord> records)
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var record in records)
            {
                var index = _items.FindIndex(item => item.Id == record.Id);
                if (index >= 0 && record.UpdatedAt <= _items[index].UpdatedAt)
                {
                    continue;
                }

                SaveLocked(record, keepCreated: false);
                changed = true;
            }

            if (changed)
            {
                Persist();
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void QuitarDesdeNube(IReadOnlyCollection<Guid> ids)
    {
        var changed = false;
        lock (_gate)
        {
            changed = _items.RemoveAll(item => ids.Contains(item.Id)) > 0;
            if (changed)
            {
                Persist();
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private void SaveLocked(EquipmentRecord record, bool keepCreated)
    {
        var index = _items.FindIndex(item => item.Id == record.Id);
        if (index >= 0)
        {
            if (keepCreated)
            {
                record.CreatedAt = _items[index].CreatedAt;
            }

            _items[index] = record;
            return;
        }

        if (record.CreatedAt == default)
        {
            record.CreatedAt = record.UpdatedAt == default ? DateTimeOffset.Now : record.UpdatedAt;
        }

        _items.Add(record);
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize(json, InventoryJsonContext.Default.ListEquipmentRecord);
            if (items is null)
            {
                return;
            }

            lock (_gate)
            {
                _items.AddRange(items);
            }
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_items, InventoryJsonContext.Default.ListEquipmentRecord);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, _path, overwrite: true);
    }

    private static string ResolvePath()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "inventory.json");
        }
        catch (Exception)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HardwareRegistro");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "inventory.json");
        }
    }
}
