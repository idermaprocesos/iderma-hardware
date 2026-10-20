using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace HardwareRegistro.Models;

public enum EquipmentKind
{
    Pc,
    Laptop,
    Monitor,
    Phone,
    Mouse,
    Cable
}

public sealed class EquipmentComponent
{
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class EquipmentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InternalCode { get; set; } = "";
    public EquipmentKind Kind { get; set; }
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Location { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public string Status { get; set; } = EquipmentCatalog.DefaultStatus;
    public string Notes { get; set; } = "";
    public List<EquipmentComponent> Components { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public readonly record struct ComponentPreset(string Name, string Hint);

public sealed class ComponentEntry : INotifyPropertyChanged
{
    private string _name = "";
    private string _detail = "";
    private EquipmentKind _kind;

    public EquipmentKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hint)));
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hint)));
        }
    }

    public string Detail
    {
        get => _detail;
        set => Set(ref _detail, value);
    }

    public string Hint => EquipmentCatalog.HintFor(Kind, Name);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class EquipmentEditorArgs
{
    public Guid? EquipmentId { get; init; }
    public EquipmentKind? Kind { get; init; }
}

public static class EquipmentCatalog
{
    public const string DefaultStatus = "Disponible";

    public static IReadOnlyList<string> Statuses { get; } =
    [
        "Disponible",
        "En uso",
        "En reparación",
        "De baja"
    ];

    public static string Name(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Pc => "PC",
        EquipmentKind.Laptop => "Laptop",
        EquipmentKind.Monitor => "Monitor",
        EquipmentKind.Phone => "Teléfono",
        EquipmentKind.Mouse => "Mouse",
        EquipmentKind.Cable => "Cable",
        _ => "Equipo"
    };

    public static string PluralName(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Pc => "PCs",
        EquipmentKind.Laptop => "Laptops",
        EquipmentKind.Monitor => "Monitores",
        EquipmentKind.Phone => "Teléfonos",
        EquipmentKind.Mouse => "Mouses",
        EquipmentKind.Cable => "Cables",
        _ => "Equipos"
    };

    public static string Glyph(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Pc => "\uE977",
        EquipmentKind.Laptop => "\uE7F8",
        EquipmentKind.Monitor => "\uE7F4",
        EquipmentKind.Phone => "\uE8EA",
        EquipmentKind.Mouse => "\uE962",
        EquipmentKind.Cable => "\uE839",
        _ => "\uE977"
    };

    public static string Prefix(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Pc => "PC",
        EquipmentKind.Laptop => "LAP",
        EquipmentKind.Monitor => "MON",
        EquipmentKind.Phone => "TEL",
        EquipmentKind.Mouse => "MOU",
        EquipmentKind.Cable => "CAB",
        _ => "EQ"
    };

    public static IReadOnlyList<ComponentPreset> Presets(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Pc =>
        [
            new("Procesador", "Intel Core i5-12400"),
            new("Memoria RAM", "16 GB DDR4"),
            new("Almacenamiento", "SSD 512 GB"),
            new("Tarjeta gráfica", "NVIDIA GTX 1650"),
            new("Placa base", "ASUS Prime B660"),
            new("Fuente de alimentación", "500 W 80 Plus"),
            new("Sistema operativo", "Windows 11 Pro"),
            new("Gabinete", "Torre mediana")
        ],
        EquipmentKind.Laptop =>
        [
            new("Procesador", "Intel Core i7-1355U"),
            new("Memoria RAM", "16 GB LPDDR5"),
            new("Almacenamiento", "SSD 512 GB"),
            new("Pantalla", "15.6 pulgadas Full HD"),
            new("Tarjeta gráfica", "Intel Iris Xe"),
            new("Batería", "56 Wh"),
            new("Sistema operativo", "Windows 11 Home")
        ],
        EquipmentKind.Monitor =>
        [
            new("Tamaño", "24 pulgadas"),
            new("Resolución", "1920 × 1080"),
            new("Tipo de panel", "IPS"),
            new("Tasa de refresco", "75 Hz"),
            new("Puertos", "HDMI y DisplayPort"),
            new("Soporte", "Altura ajustable")
        ],
        EquipmentKind.Phone =>
        [
            new("Procesador", "Snapdragon 7 Gen 3"),
            new("Memoria RAM", "8 GB"),
            new("Almacenamiento", "128 GB"),
            new("Pantalla", "6.5 pulgadas OLED"),
            new("Batería", "5000 mAh"),
            new("Sistema operativo", "Android 15"),
            new("IMEI", "356938035643809")
        ],
        EquipmentKind.Mouse =>
        [
            new("Conexión", "USB o Bluetooth"),
            new("Sensor", "Óptico"),
            new("DPI", "1600"),
            new("Botones", "3"),
            new("Color", "Negro")
        ],
        EquipmentKind.Cable =>
        [
            new("Tipo", "HDMI 2.0"),
            new("Longitud", "1.8 m"),
            new("Conector origen", "HDMI macho"),
            new("Conector destino", "HDMI macho"),
            new("Categoría", "Video")
        ],
        _ => []
    };

    public static string HintFor(EquipmentKind kind, string name)
    {
        foreach (var preset in Presets(kind))
        {
            if (string.Equals(preset.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return preset.Hint;
            }
        }

        return "Detalle";
    }

    public static string NextCode(EquipmentKind kind, IEnumerable<EquipmentRecord> existing)
    {
        var prefix = Prefix(kind);
        var head = prefix + "-";
        var max = 0;

        foreach (var item in existing)
        {
            if (item.Kind != kind)
            {
                continue;
            }

            var code = item.InternalCode.Trim();
            if (code.StartsWith(head, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code[head.Length..], out var number))
            {
                max = Math.Max(max, number);
            }
        }

        return $"{prefix}-{(max + 1):000}";
    }
}

public sealed class EquipmentItem
{
    public EquipmentItem(EquipmentRecord record)
    {
        Record = record;
        Headline = BuildHeadline(record);
        Meta = BuildMeta(record);
        ComponentsLine = BuildComponents(record);
    }

    public EquipmentRecord Record { get; }
    public string Code => Record.InternalCode;
    public string KindName => EquipmentCatalog.Name(Record.Kind);
    public string Glyph => EquipmentCatalog.Glyph(Record.Kind);
    public string Status => string.IsNullOrWhiteSpace(Record.Status) ? EquipmentCatalog.DefaultStatus : Record.Status;
    public string Headline { get; }
    public string Meta { get; }
    public string ComponentsLine { get; }

    private static string BuildHeadline(EquipmentRecord record)
    {
        var name = $"{record.Brand} {record.Model}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "Sin marca ni modelo" : name;
    }

    private static string BuildMeta(EquipmentRecord record)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(record.Location))
        {
            parts.Add(record.Location.Trim());
        }

        if (!string.IsNullOrWhiteSpace(record.AssignedTo))
        {
            parts.Add(record.AssignedTo.Trim());
        }

        if (!string.IsNullOrWhiteSpace(record.SerialNumber))
        {
            parts.Add("Serie " + record.SerialNumber.Trim());
        }

        return parts.Count == 0 ? "Sin ubicación asignada" : string.Join(" · ", parts);
    }

    private static string BuildComponents(EquipmentRecord record)
    {
        var filled = record.Components
            .Where(component => !string.IsNullOrWhiteSpace(component.Name) || !string.IsNullOrWhiteSpace(component.Detail))
            .ToList();

        if (filled.Count == 0)
        {
            return "Sin componentes registrados";
        }

        var shown = filled.Take(3).Select(component =>
        {
            var name = string.IsNullOrWhiteSpace(component.Name) ? "Componente" : component.Name.Trim();
            var detail = component.Detail.Trim();
            return string.IsNullOrWhiteSpace(detail) ? name : $"{name}: {detail}";
        });

        var text = string.Join(" · ", shown);
        var extra = filled.Count - 3;
        return extra > 0 ? $"{text} · +{extra}" : text;
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(EquipmentRecord))]
[JsonSerializable(typeof(EquipmentComponent))]
[JsonSerializable(typeof(List<EquipmentRecord>))]
public partial class InventoryJsonContext : JsonSerializerContext
{
}
