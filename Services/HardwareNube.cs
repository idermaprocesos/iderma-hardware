using Google.Cloud.Firestore;
using Iderma.Core.Services;
using HardwareRegistro.Models;

namespace HardwareRegistro.Services;

public static class HardwareNube
{
    private const string Coleccion = "hardware";
    private const string ColeccionBajas = "hardware-bajas";
    private const string Cursor = "hardware";
    private const string CursorBajas = "hardware-bajas";

    private static readonly SemaphoreSlim Puerta = new(1, 1);
    private static FirestoreDb? _base;
    private static FirestoreChangeListener? _escucha;
    private static FirestoreChangeListener? _escuchaBajas;
    private static int _iniciado;
    private static string _estado = "Inventario local";

    public static string Estado => _estado;

    public static event Action? EstadoCambiado;

    public static void Iniciar()
    {
        if (Interlocked.Exchange(ref _iniciado, 1) == 1)
        {
            return;
        }

        _ = ConectarAsync();
    }

    public static void Publicar(EquipmentRecord record)
    {
        var copia = Clonar(record);
        _ = EscribirAsync(copia);
    }

    public static void PublicarBaja(Guid id, DateTimeOffset cuando)
    {
        _ = BorrarAsync(id, cuando);
    }

    private static async Task ConectarAsync()
    {
        try
        {
            CambiarEstado("Conectando el inventario con Firestore…");
            await AsegurarBaseAsync();
            if (_base is null || _escucha is not null)
            {
                if (_base is null)
                {
                    CambiarEstado("Inventario local: no se encontraron las credenciales de Firestore.");
                }

                return;
            }

            var snap = await _base.Collection(Coleccion).GetSnapshotAsync();
            var remotos = snap.Documents.Select(Leer).OfType<EquipmentRecord>().ToList();
            var locales = InventoryStore.Instance.Items;
            var idsNube = remotos.Select(item => item.Id).ToHashSet();
            var aplicar = new List<EquipmentRecord>();
            var subir = new List<EquipmentRecord>();

            foreach (var remoto in remotos)
            {
                var local = locales.FirstOrDefault(item => item.Id == remoto.Id);
                if (local is null || remoto.UpdatedAt > local.UpdatedAt)
                {
                    aplicar.Add(remoto);
                }
                else if (local.UpdatedAt > remoto.UpdatedAt && !Igual(local, remoto))
                {
                    subir.Add(local);
                }
            }

            foreach (var local in locales)
            {
                if (!idsNube.Contains(local.Id))
                {
                    subir.Add(local);
                }
            }

            if (aplicar.Count > 0)
            {
                InventoryStore.Instance.AplicarDesdeNube(aplicar);
            }

            var bajas = await _base.Collection(ColeccionBajas).GetSnapshotAsync();
            QuitarSiLaBajaEsPosterior(bajas.Documents);

            await EscribirLoteAsync(_base, subir);

            var desde = snap.ReadTime;
            var desdeBajas = bajas.ReadTime;
            CursorNube.Guardar(Cursor, desde);
            CursorNube.Guardar(CursorBajas, desdeBajas);

            _escucha = _base.Collection(Coleccion)
                .WhereGreaterThan("Actualizado", desde)
                .OrderBy("Actualizado")
                .Listen(Recibir);
            _escuchaBajas = _base.Collection(ColeccionBajas)
                .WhereGreaterThan("Actualizado", desdeBajas)
                .OrderBy("Actualizado")
                .Listen(RecibirBajas);

            CambiarEstado("Inventario en vivo con Firestore");
        }
        catch (Exception ex)
        {
            CambiarEstado("Inventario local: no hay conexión con Firestore. " + ex.Message);
        }
    }

    private static void Recibir(QuerySnapshot snapshot)
    {
        try
        {
            var aplicar = new List<EquipmentRecord>();
            foreach (var cambio in snapshot.Changes)
            {
                if (cambio.ChangeType == DocumentChange.Type.Removed || !cambio.Document.Exists)
                {
                    continue;
                }

                var record = Leer(cambio.Document);
                if (record is not null)
                {
                    aplicar.Add(record);
                }
            }

            if (aplicar.Count > 0)
            {
                InventoryStore.Instance.AplicarDesdeNube(aplicar);
            }

            CursorNube.GuardarSiAvanza(Cursor, snapshot.ReadTime);
        }
        catch
        {
        }
    }

    private static void RecibirBajas(QuerySnapshot snapshot)
    {
        try
        {
            QuitarSiLaBajaEsPosterior(snapshot.Documents);

            CursorNube.GuardarSiAvanza(CursorBajas, snapshot.ReadTime);
        }
        catch
        {
        }
    }

    private static async Task EscribirAsync(EquipmentRecord record)
    {
        try
        {
            await AsegurarBaseAsync();
            if (_base is null)
            {
                return;
            }

            await Puerta.WaitAsync();
            try
            {
                await EscribirDocumentoAsync(_base, record);
            }
            finally
            {
                Puerta.Release();
            }
        }
        catch
        {
        }
    }

    private static async Task BorrarAsync(Guid id, DateTimeOffset cuando)
    {
        try
        {
            await AsegurarBaseAsync();
            if (_base is null)
            {
                return;
            }

            await Puerta.WaitAsync();
            try
            {
                var clave = id.ToString("D");
                var lote = _base.StartBatch();
                lote.Delete(_base.Collection(Coleccion).Document(clave));
                lote.Set(_base.Collection(ColeccionBajas).Document(clave), new Dictionary<string, object>
                {
                    ["Actualizado"] = Timestamp.FromDateTimeOffset(cuando.ToUniversalTime())
                });
                await lote.CommitAsync();
            }
            finally
            {
                Puerta.Release();
            }
        }
        catch
        {
        }
    }

    private static void QuitarSiLaBajaEsPosterior(IEnumerable<DocumentSnapshot> documentos)
    {
        var quitar = new List<Guid>();
        foreach (var documento in documentos)
        {
            if (!documento.Exists || !Guid.TryParse(documento.Id, out var id) || !documento.ContainsField("Actualizado"))
            {
                continue;
            }

            var cuando = documento.GetValue<Timestamp>("Actualizado").ToDateTimeOffset();
            var local = InventoryStore.Instance.Find(id);
            if (local is null || cuando >= local.UpdatedAt)
            {
                quitar.Add(id);
            }
        }

        if (quitar.Count > 0)
        {
            InventoryStore.Instance.QuitarDesdeNube(quitar);
        }
    }

    private static Task EscribirDocumentoAsync(FirestoreDb db, EquipmentRecord record)
    {
        return db.Collection(Coleccion).Document(record.Id.ToString("D")).SetAsync(ADocumento(record));
    }

    private static async Task EscribirLoteAsync(FirestoreDb db, IReadOnlyList<EquipmentRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        const int tamano = 400;
        var coleccion = db.Collection(Coleccion);
        for (var inicio = 0; inicio < records.Count; inicio += tamano)
        {
            var lote = db.StartBatch();
            var fin = Math.Min(inicio + tamano, records.Count);
            for (var i = inicio; i < fin; i++)
            {
                var record = records[i];
                lote.Set(coleccion.Document(record.Id.ToString("D")), ADocumento(record));
            }

            await lote.CommitAsync();
        }
    }

    private static async Task AsegurarBaseAsync()
    {
        if (_base is not null)
        {
            return;
        }

        await Puerta.WaitAsync();
        try
        {
            if (_base is not null)
            {
                return;
            }

            _base = await ConexionFirebase.AbrirAsync();
        }
        finally
        {
            Puerta.Release();
        }
    }

    private static Dictionary<string, object> ADocumento(EquipmentRecord record)
    {
        return new Dictionary<string, object>
        {
            ["Codigo"] = record.InternalCode,
            ["Tipo"] = record.Kind.ToString(),
            ["Marca"] = record.Brand,
            ["Modelo"] = record.Model,
            ["Serie"] = record.SerialNumber,
            ["Ubicacion"] = record.Location,
            ["Asignado"] = record.AssignedTo,
            ["Estado"] = record.Status,
            ["Notas"] = record.Notes,
            ["Componentes"] = record.Components
                .Select(component => (object)new Dictionary<string, object>
                {
                    ["Nombre"] = component.Name,
                    ["Detalle"] = component.Detail
                })
                .ToList(),
            ["Creado"] = Marca(record.CreatedAt),
            ["Actualizado"] = Marca(record.UpdatedAt)
        };
    }

    private static EquipmentRecord? Leer(DocumentSnapshot documento)
    {
        if (!documento.Exists || !Guid.TryParse(documento.Id, out var id))
        {
            return null;
        }

        if (!Enum.TryParse<EquipmentKind>(Texto(documento, "Tipo"), out var tipo))
        {
            tipo = EquipmentKind.Pc;
        }

        return new EquipmentRecord
        {
            Id = id,
            InternalCode = Texto(documento, "Codigo"),
            Kind = tipo,
            Brand = Texto(documento, "Marca"),
            Model = Texto(documento, "Modelo"),
            SerialNumber = Texto(documento, "Serie"),
            Location = Texto(documento, "Ubicacion"),
            AssignedTo = Texto(documento, "Asignado"),
            Status = string.IsNullOrWhiteSpace(Texto(documento, "Estado"))
                ? EquipmentCatalog.DefaultStatus
                : Texto(documento, "Estado"),
            Notes = Texto(documento, "Notas"),
            Components = Componentes(documento),
            CreatedAt = Fecha(documento, "Creado"),
            UpdatedAt = Fecha(documento, "Actualizado")
        };
    }

    private static List<EquipmentComponent> Componentes(DocumentSnapshot documento)
    {
        if (!documento.ContainsField("Componentes"))
        {
            return [];
        }

        if (documento.GetValue<object>("Componentes") is not IEnumerable<object> lista)
        {
            return [];
        }

        var components = new List<EquipmentComponent>();
        foreach (var item in lista)
        {
            if (item is not IDictionary<string, object> mapa)
            {
                continue;
            }

            components.Add(new EquipmentComponent
            {
                Name = mapa.TryGetValue("Nombre", out var nombre) ? nombre?.ToString() ?? "" : "",
                Detail = mapa.TryGetValue("Detalle", out var detalle) ? detalle?.ToString() ?? "" : ""
            });
        }

        return components;
    }

    private static string Texto(DocumentSnapshot documento, string campo)
    {
        return documento.ContainsField(campo) ? documento.GetValue<string>(campo) ?? "" : "";
    }

    private static DateTimeOffset Fecha(DocumentSnapshot documento, string campo)
    {
        if (!documento.ContainsField(campo))
        {
            return DateTimeOffset.Now;
        }

        return documento.GetValue<Timestamp>(campo).ToDateTimeOffset();
    }

    private static Timestamp Marca(DateTimeOffset momento)
    {
        if (momento == default)
        {
            momento = DateTimeOffset.UtcNow;
        }

        return Timestamp.FromDateTimeOffset(momento.ToUniversalTime());
    }

    private static EquipmentRecord Clonar(EquipmentRecord record)
    {
        return new EquipmentRecord
        {
            Id = record.Id,
            InternalCode = record.InternalCode,
            Kind = record.Kind,
            Brand = record.Brand,
            Model = record.Model,
            SerialNumber = record.SerialNumber,
            Location = record.Location,
            AssignedTo = record.AssignedTo,
            Status = record.Status,
            Notes = record.Notes,
            Components = record.Components
                .Select(component => new EquipmentComponent { Name = component.Name, Detail = component.Detail })
                .ToList(),
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }

    private static bool Igual(EquipmentRecord left, EquipmentRecord right)
    {
        if (left.Id != right.Id
            || !string.Equals(left.InternalCode, right.InternalCode, StringComparison.Ordinal)
            || left.Kind != right.Kind
            || !string.Equals(left.Brand, right.Brand, StringComparison.Ordinal)
            || !string.Equals(left.Model, right.Model, StringComparison.Ordinal)
            || !string.Equals(left.SerialNumber, right.SerialNumber, StringComparison.Ordinal)
            || !string.Equals(left.Location, right.Location, StringComparison.Ordinal)
            || !string.Equals(left.AssignedTo, right.AssignedTo, StringComparison.Ordinal)
            || !string.Equals(left.Status, right.Status, StringComparison.Ordinal)
            || !string.Equals(left.Notes, right.Notes, StringComparison.Ordinal)
            || left.Components.Count != right.Components.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Components.Count; i++)
        {
            if (!string.Equals(left.Components[i].Name, right.Components[i].Name, StringComparison.Ordinal)
                || !string.Equals(left.Components[i].Detail, right.Components[i].Detail, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void CambiarEstado(string estado)
    {
        _estado = estado;
        EstadoCambiado?.Invoke();
    }

}
