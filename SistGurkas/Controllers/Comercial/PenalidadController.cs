using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistGurkas.Models.Comercial;
using System.Data;

namespace SistGurkas.Controllers.Comercial;

public class PenalidadController(IConfiguration cfg, IWebHostEnvironment env) : Controller
{
    SqlConnection C() => new(cfg.GetConnectionString("SistGurkas")!);
    static SqlCommand S(SqlConnection c, string n) => new(n, c)
    {
        CommandType = CommandType.StoredProcedure
    };

    static void P(SqlCommand c, string n, object? v) => c.Parameters.AddWithValue(n, v ?? DBNull.Value);
    static string Str(SqlDataReader r, string n) => r[n] == DBNull.Value ? "" : r[n].ToString() ?? "";
    static int Int(SqlDataReader r, string n) => r[n] == DBNull.Value ? 0 : Convert.ToInt32(r[n]);
    static decimal Dec(SqlDataReader r, string n) => r[n] == DBNull.Value ? 0 : Convert.ToDecimal(r[n]);
    static bool Has(SqlDataReader r, string n)
    {
        for (int i = 0; i < r.FieldCount; i++)
            if (string.Equals(r.GetName(i), n, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static int IntSafe(SqlDataReader r, string n) => !Has(r, n) || r[n] == DBNull.Value ? 0 : Convert.ToInt32(r[n]);
    static int? NIntSafe(SqlDataReader r, string n) => !Has(r, n) || r[n] == DBNull.Value ? null : Convert.ToInt32(r[n]);

    public async Task<IActionResult> Index()
    {
        var vm = new PenalidadDashboardVm();
        await using var cn = C();
        await cn.OpenAsync();
        await using var cmd = S(cn, "dbo.sp_PenalidadReal_Dashboard");
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            vm.MontoTotal = Dec(r, "MontoTotal");
            vm.Registradas = Int(r, "Registradas");
            vm.Anuladas = Int(r, "Anuladas");
            vm.EnAnalisis = Int(r, "EnAnalisis");
            vm.PendientesPago = Int(r, "PendientesPago");
            vm.Pagadas = Int(r, "Pagadas");
        }

        if (await r.NextResultAsync())
            while (await r.ReadAsync())
                vm.PorMes.Add(new()
                {
                    Etiqueta = Str(r, "Etiqueta"),
                    Valor = Dec(r, "Valor"),
                    Cantidad = Int(r, "Cantidad")
                });
        if (await r.NextResultAsync())
            while (await r.ReadAsync())
                vm.PorCliente.Add(new()
                {
                    Etiqueta = Str(r, "Etiqueta"),
                    Valor = Dec(r, "Valor"),
                    Cantidad = Int(r, "Cantidad")
                });
        if (await r.NextResultAsync())
            while (await r.ReadAsync())
                vm.PorCategoria.Add(new()
                {
                    Etiqueta = Str(r, "Etiqueta"),
                    Valor = Dec(r, "Valor"),
                    Cantidad = Int(r, "Cantidad")
                });
        if (await r.NextResultAsync())
            while (await r.ReadAsync())
                vm.Penalidades.Add(MapReal(r));
        return View("~/Views/Comercial/Penalidad/Index.cshtml", vm);
    }

    public async Task<IActionResult> Nueva()
    {
        var vm = new PenalidadNuevaVm();
        await using var cn = C();
        await cn.OpenAsync();
        await using var cmd = S(cn, "dbo.sp_PenalidadReal_Catalogos");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            vm.Clientes.Add(new()
            {
                IdUnidad = Int(r, "IdUnidad"),
                RazonSocial = Str(r, "RazonSocial"),
                NombreComercial = Str(r, "NombreComercial")
            });
        if (await r.NextResultAsync())
            while (await r.ReadAsync())
                vm.Contratos.Add(new()
                {
                    IdContrato = Int(r, "IdContrato"),
                    IdUnidad = Int(r, "IdUnidad"),
                    NumeroContrato = Str(r, "NumeroContrato"),
                    Descripcion = Str(r, "Descripcion")
                });
        if (await r.NextResultAsync())
            while (await r.ReadAsync())
                vm.Sedes.Add(new()
                {
                    IdSede = Int(r, "IdSede"),
                    IdUnidad = Int(r, "IdUnidad"),
                    NombreSede = Str(r, "NombreSede")
                });
        if (await r.NextResultAsync())
            while (await r.ReadAsync())
                vm.Tipos.Add(Map(r));
        // El SP contractual histórico no siempre devuelve los IDs relacionales.
        // Para el modal de registro inferimos IdUnidad por el nombre del cliente sin romper PorCliente/Tipos.
        foreach (var tipo in vm.Tipos.Where(x => x.IdUnidad == 0 && !string.IsNullOrWhiteSpace(x.Cliente)))
        {
            var cli = vm.Clientes.FirstOrDefault(x =>
                string.Equals(x.RazonSocial?.Trim(), tipo.Cliente.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.NombreComercial?.Trim(), tipo.Cliente.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cli != null)
                tipo.IdUnidad = cli.IdUnidad;
        }

        return PartialView("~/Views/Comercial/Penalidad/Nueva.cshtml", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Registrar(
        int idUnidad,
        int idContrato,
        int idSede,
        int idPenalidadCliente,
        DateTime fecha,
        string numeroDocumento,
        int cantidad,
        string descripcion,
        string responsable,
        DateTime? fechaLimitePago,
        string? observacion,
        IFormFile? carta)
    {
        string? ruta = null;
        if (carta is { Length: > 0 })
        {
            var dir = Path.Combine(env.WebRootPath, "uploads", "penalidades");
            Directory.CreateDirectory(dir);
            var fn = $"{Guid.NewGuid():N}{Path.GetExtension(carta.FileName)}";
            await using var fs = System.IO.File.Create(Path.Combine(dir, fn));
            await carta.CopyToAsync(fs);
            ruta = $"/uploads/penalidades/{fn}";
        }

        await using var cn = C();
        await cn.OpenAsync();
        await using var cmd = S(cn, "dbo.sp_PenalidadReal_Registrar");
        P(cmd, "@IdUnidad", idUnidad);
        P(cmd, "@IdContrato", idContrato);
        P(cmd, "@IdSede", idSede);
        P(cmd, "@IdPenalidadCliente", idPenalidadCliente);
        P(cmd, "@Fecha", fecha);
        P(cmd, "@NumeroDocumento", numeroDocumento);
        P(cmd, "@Cantidad", cantidad);
        P(cmd, "@Descripcion", descripcion);
        P(cmd, "@Responsable", responsable);
        P(cmd, "@FechaLimitePago", fechaLimitePago);
        P(cmd, "@Observacion", observacion);
        P(cmd, "@CartaRuta", ruta);
        P(cmd, "@CartaNombre", carta?.FileName);
        await cmd.ExecuteNonQueryAsync();
        TempData["Ok"] = "Penalidad registrada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Tipos()
    {
        var l = new List<TipoPenalidad>();
        await using var cn = C();
        await cn.OpenAsync();
        await using var cmd = S(cn, "dbo.sp_TipoPenalidad_Listar");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new()
            {
                IdTipoPenalidad = Int(r, "IdTipoPenalidad"),
                Codigo = Str(r, "Codigo"),
                Descripcion = Str(r, "Descripcion"),
                Categoria = Str(r, "Categoria"),
                Gravedad = Str(r, "Gravedad"),
                IdEstado = Int(r, "IdEstado")
            });
        return View("~/Views/Comercial/Penalidad/Tipos.cshtml", l);
    }

    public async Task<IActionResult> PorCliente()
    {
        var l = new List<PenalidadClienteItem>();
        await using var cn = C();
        await cn.OpenAsync();
        await using var cmd = S(cn, "dbo.sp_PenalidadCliente_Listar");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(Map(r));
        return View("~/Views/Comercial/Penalidad/PorCliente.cshtml", l);
    }

    static PenalidadClienteItem Map(SqlDataReader r) => new()
    {
        IdPenalidadCliente = Int(r, "IdPenalidadCliente"),
        IdUnidad = IntSafe(r, "IdUnidad"),
        IdContrato = NIntSafe(r, "IdContrato"),
        IdSede = NIntSafe(r, "IdSede"),
        Descripcion = Str(r, "Descripcion"),
        Categoria = Str(r, "Categoria"),
        FormaCalculo = Str(r, "FormaCalculo"),
        Valor = r["Valor"] == DBNull.Value ? null : Convert.ToDecimal(r["Valor"]),
        UnidadCalculo = Str(r, "UnidadCalculo"),
        Frecuencia = Str(r, "Frecuencia"),
        Fuente = Str(r, "Fuente"),
        AreaResponsable = Str(r, "AreaResponsable"),
        Cliente = Str(r, "Cliente"),
        Contrato = Str(r, "Contrato"),
        Sede = Str(r, "Sede"),
        IdEstado = Int(r, "IdEstado")
    };

    static PenalidadRealItem MapReal(SqlDataReader r) => new()
    {
        IdPenalidad = Int(r, "IdPenalidad"),
        Codigo = Str(r, "Codigo"),
        Fecha = Convert.ToDateTime(r["Fecha"]),
        NumeroDocumento = Str(r, "NumeroDocumento"),
        Cliente = Str(r, "Cliente"),
        Contrato = Str(r, "Contrato"),
        Sede = Str(r, "Sede"),
        TipoPenalidad = Str(r, "TipoPenalidad"),
        Categoria = Str(r, "Categoria"),
        Descripcion = Str(r, "Descripcion"),
        MontoTotal = Dec(r, "MontoTotal"),
        Estado = Str(r, "Estado"),
        Responsable = Str(r, "Responsable"),
        FechaLimitePago = r["FechaLimitePago"] == DBNull.Value ? null : Convert.ToDateTime(r["FechaLimitePago"])
    };
}
