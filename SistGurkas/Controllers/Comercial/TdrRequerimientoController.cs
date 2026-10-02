using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistGurkas.Models.Comercial;
using System.Data;

namespace SistGurkas.Controllers.Comercial;

public class TdrRequerimientoController(IConfiguration cfg) : Controller
{
    SqlConnection C() => new(cfg.GetConnectionString("SistGurkas")!);
    static SqlCommand S(SqlConnection c, string n) => new(n, c) { CommandType = CommandType.StoredProcedure };
    static void P(SqlCommand c, string n, object? v) => c.Parameters.AddWithValue(n, v ?? DBNull.Value);

    public async Task<IActionResult> Index(string? categoria = null, string? cliente = null, string? contrato = null, string? sede = null, int? estado = null)
    {
        var vm = new TdrDashboardVm { FiltroCategoria = categoria, FiltroCliente = cliente, FiltroContrato = contrato, FiltroSede = sede, FiltroEstado = estado };
        var todos = new List<TdrRequerimientoItem>();

        await using var cn = C();
        await cn.OpenAsync();
        await using var cmd = S(cn, "dbo.sp_TdrRequerimiento_Dashboard");
        P(cmd, "@Categoria", null); // El dashboard necesita el universo completo para filtros y agregaciones.
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
            vm.TotalTdr = Convert.ToInt32(r["TotalTdr"]);
        await r.NextResultAsync();
        while (await r.ReadAsync())
            todos.Add(Map(r));
        await r.CloseAsync();

        vm.Clientes = todos.Select(x => x.Cliente).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        vm.Contratos = todos.Select(x => x.Contrato).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        vm.Sedes = todos.Select(x => x.Sede).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        IEnumerable<TdrRequerimientoItem> q = todos;
        if (!string.IsNullOrWhiteSpace(cliente))
            q = q.Where(x => string.Equals(x.Cliente, cliente, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(contrato))
            q = q.Where(x => string.Equals(x.Contrato, contrato, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(sede))
            q = q.Where(x => string.Equals(x.Sede, sede, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(categoria))
            q = q.Where(x => string.Equals(x.Categoria, categoria, StringComparison.OrdinalIgnoreCase));
        if (estado.HasValue)
            q = q.Where(x => x.IdEstado == estado.Value);
        vm.Items = q.ToList();

        vm.TotalRequerimientos = vm.Items.Count;
        vm.Personal = vm.Items.Count(x => x.Categoria.Equals("PERSONAL", StringComparison.OrdinalIgnoreCase));
        vm.Equipos = vm.Items.Count(x => x.Categoria.Equals("EQUIPO", StringComparison.OrdinalIgnoreCase));
        vm.Documentacion = vm.Items.Count(x => x.Categoria.Equals("DOCUMENTACION", StringComparison.OrdinalIgnoreCase));
        vm.Activos = vm.Items.Count(x => x.IdEstado == 1);
        vm.Inactivos = vm.Items.Count - vm.Activos;
        vm.PorCliente = Resumir(vm.Items, x => x.Cliente, 5);
        vm.PorSede = Resumir(vm.Items.Where(x => !string.IsNullOrWhiteSpace(x.Sede)), x => x.Sede, 5);
        vm.PorArea = ResumirAreas(vm.Items, 6);

        // KPIs operativos reales: alimentados exclusivamente por SP.
        await using (var resumen = S(cn, "dbo.sp_TdrCumplimiento_Resumen"))
        await using (var cr = await resumen.ExecuteReaderAsync())
        {
            // Resultset 1: sp_TdrCumplimiento_Inicializar (Inicializados).
            await cr.NextResultAsync();

            // Resultset 2: resumen general.
            if (await cr.ReadAsync())
            {
                vm.TotalRequerimientos = Convert.ToInt32(cr["TotalRequerimientos"]);
                vm.CumplimientoGeneral = cr["CumplimientoGeneral"] == DBNull.Value ? 0 : Convert.ToDecimal(cr["CumplimientoGeneral"]);
                vm.Cumplidos = Convert.ToInt32(cr["Cumplidos"]);
                vm.EnProceso = Convert.ToInt32(cr["EnProceso"]);
                vm.Pendientes = Convert.ToInt32(cr["Pendientes"]);
                vm.NoAplica = Convert.ToInt32(cr["NoAplica"]);
            }

            // Resultset 3: cumplimiento por categoría.
            await cr.NextResultAsync();
            while (await cr.ReadAsync())
                vm.CumplimientoPorTipo.Add(new TdrCumplimientoResumenItem
                {
                    Nombre = cr["Categoria"].ToString() ?? "",
                    Total = Convert.ToInt32(cr["Total"]),
                    Porcentaje = cr["Porcentaje"] == DBNull.Value ? 0 : Convert.ToDecimal(cr["Porcentaje"])
                });

            // Resultset 4: cumplimiento por sede.
            await cr.NextResultAsync();
            while (await cr.ReadAsync())
                vm.CumplimientoPorSede.Add(new TdrCumplimientoResumenItem
                {
                    Nombre = cr["Sede"].ToString() ?? "Sin sede",
                    Total = Convert.ToInt32(cr["Total"]),
                    Porcentaje = cr["Porcentaje"] == DBNull.Value ? 0 : Convert.ToDecimal(cr["Porcentaje"])
                });

            // Resultset 5: pendientes por área.
            await cr.NextResultAsync();
            var pendientesAreaRaw = new List<TdrPendienteAreaItem>();
            while (await cr.ReadAsync())
                pendientesAreaRaw.Add(new TdrPendienteAreaItem
                {
                    Area = cr["AreaResponsable"].ToString() ?? "Sin área",
                    Pendientes = Convert.ToInt32(cr["Pendientes"])
                });

            // Un requerimiento puede pertenecer a varias áreas (ej. RRHH|OPERACIONES).
            vm.PendientesPorArea = pendientesAreaRaw
                .SelectMany(x => (x.Area ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(a => new { Area = a, x.Pendientes }))
                .GroupBy(x => x.Area, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TdrPendienteAreaItem { Area = g.Key, Pendientes = g.Sum(x => x.Pendientes) })
                .OrderByDescending(x => x.Pendientes)
                .ToList();

            // Resultset 6: evolución histórica.
            await cr.NextResultAsync();
            while (await cr.ReadAsync())
                vm.Evolucion.Add(new TdrEvolucionItem
                {
                    Periodo = cr["Periodo"].ToString() ?? "",
                    Porcentaje = cr["Porcentaje"] == DBNull.Value ? 0 : Convert.ToDecimal(cr["Porcentaje"])
                });
        }

        return View("~/Views/Comercial/TdrRequerimiento/Index.cshtml", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, int estado)
    {
        await using var cn = C();
        await cn.OpenAsync();
        await using var cmd = S(cn, "dbo.sp_TdrRequerimiento_CambiarEstado");
        P(cmd, "@IdTdrRequerimiento", id);
        P(cmd, "@IdEstado", estado);
        await cmd.ExecuteNonQueryAsync();
        return Json(new
        {
            ok = true
        });
    }

    static List<TdrResumenItem> Resumir(IEnumerable<TdrRequerimientoItem> items, Func<TdrRequerimientoItem, string> key, int take)
    {
        var data = items.Where(x => !string.IsNullOrWhiteSpace(key(x))).GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TdrResumenItem { Nombre = g.Key, Total = g.Count(), Personal = g.Count(x => x.Categoria == "PERSONAL"), Equipos = g.Count(x => x.Categoria == "EQUIPO"), Documentacion = g.Count(x => x.Categoria == "DOCUMENTACION") })
            .OrderByDescending(x => x.Total).Take(take).ToList();
        var max = data.Count == 0 ? 1 : data.Max(x => x.Total);
        foreach (var x in data)
            x.Porcentaje = x.Total * 100d / max;
        return data;
    }

    static List<TdrResumenItem> ResumirAreas(IEnumerable<TdrRequerimientoItem> items, int take)
    {
        var areas = items.SelectMany(x => (x.AreaResponsable ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TdrResumenItem { Nombre = g.Key, Total = g.Count() }).OrderByDescending(x => x.Total).Take(take).ToList();
        var max = areas.Count == 0 ? 1 : areas.Max(x => x.Total);
        foreach (var x in areas)
            x.Porcentaje = x.Total * 100d / max;
        return areas;
    }

    static TdrRequerimientoItem Map(SqlDataReader r) => new()
    {
        IdTdrRequerimiento = Convert.ToInt32(r["IdTdrRequerimiento"]),
        Categoria = r["Categoria"].ToString() ?? "",
        Subcategoria = r["Subcategoria"].ToString() ?? "",
        Descripcion = r["Descripcion"].ToString() ?? "",
        Cantidad = Convert.ToInt32(r["Cantidad"]),
        UnidadMedida = r["UnidadMedida"].ToString() ?? "",
        AreaResponsable = r["AreaResponsable"].ToString() ?? "",
        Fuente = r["Fuente"].ToString() ?? "",
        Cliente = r["Cliente"].ToString() ?? "",
        Contrato = r["Contrato"].ToString() ?? "",
        Sede = r["Sede"].ToString() ?? "",
        IdEstado = Convert.ToInt32(r["IdEstado"])
    };
}
