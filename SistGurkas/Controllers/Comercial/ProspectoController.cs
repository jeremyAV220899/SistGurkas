using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistGurkas.Models.Comercial;
using System.Data;

namespace SistGurkas.Controllers.Comercial
{
    public class ProspectoController : Controller
    {
        private readonly IConfiguration _configuration;
        public ProspectoController(IConfiguration configuration) => _configuration = configuration;
        private SqlConnection CrearConexion() => new(_configuration.GetConnectionString("SistGurkas") ?? throw new InvalidOperationException("No se encontró la conexión SistGurkas."));

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, int? idEstadoProspecto, string? origen, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            buscar = Limpiar(buscar);
            origen = Limpiar(origen);
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date)
            {
                TempData["Error"] = "La fecha desde no puede ser mayor que la fecha hasta.";
                return RedirectToAction(nameof(Index));
            }
            var prospectos = new List<Prospecto>();
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using (var cmd = SP(cn, "dbo.sp_Prospecto_Listar"))
            {
                P(cmd, "@Buscar", SqlDbType.NVarChar, buscar, 200);
                P(cmd, "@IdEstadoProspecto", SqlDbType.Int, idEstadoProspecto);
                P(cmd, "@Origen", SqlDbType.NVarChar, origen, 100);
                P(cmd, "@FechaDesde", SqlDbType.DateTime2, fechaDesde?.Date);
                P(cmd, "@FechaHastaExclusiva", SqlDbType.DateTime2, fechaHasta?.Date.AddDays(1));
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    prospectos.Add(Map(r));
            }
            ViewBag.Buscar = buscar;
            ViewBag.IdEstadoProspecto = idEstadoProspecto;
            ViewBag.Origen = origen;
            ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
            ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");
            await CargarCatalogosAsync();
            return View("~/Views/Comercial/Prospecto/Index.cshtml", prospectos);
        }

        private async Task CargarCatalogosAsync()
        {
            var sectores = new List<dynamic>();
            var actividades = new List<dynamic>();
            var departamentos = new List<dynamic>();
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Prospecto_Catalogos");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                sectores.Add(new
                {
                    Id = Convert.ToInt32(r["Id"]),
                    Nombre = r["Nombre"].ToString() ?? ""
                });
            await r.NextResultAsync();
            while (await r.ReadAsync())
                actividades.Add(new
                {
                    Id = Convert.ToInt32(r["Id"]),
                    Nombre = r["Nombre"].ToString() ?? ""
                });
            await r.NextResultAsync();
            while (await r.ReadAsync())
                departamentos.Add(new
                {
                    Id = Convert.ToInt32(r["Id"]),
                    Nombre = r["Nombre"].ToString() ?? ""
                });
            ViewBag.Sectores = sectores;
            ViewBag.Actividades = actividades;
            ViewBag.Departamentos = departamentos;
        }

        [HttpGet]
        public async Task<IActionResult> Provincias(int idDepartamento)
        {
            var x = new List<object>();
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using var c = SP(cn, "dbo.sp_Ubigeo_Provincias_Listar");
            P(c, "@IdDepartamento", SqlDbType.Int, idDepartamento);
            await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync())
                x.Add(new
                {
                    id = Convert.ToInt32(r["IdProvincia"]),
                    nombre = r["Nombre"].ToString()
                });
            return Json(x);
        }
        [HttpGet]
        public async Task<IActionResult> Distritos(int idProvincia)
        {
            var x = new List<object>();
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using var c = SP(cn, "dbo.sp_Ubigeo_Distritos_Listar");
            P(c, "@IdProvincia", SqlDbType.Int, idProvincia);
            await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync())
                x.Add(new
                {
                    id = Convert.ToInt32(r["IdDistrito"]),
                    nombre = r["Nombre"].ToString(),
                    ubigeo = r["CodigoUbigeo"].ToString()
                });
            return Json(x);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Prospecto p, List<string>? contactoNombre, List<string>? contactoCargo, List<string>? contactoTelefono, List<string>? contactoCorreo, List<int>? contactoPrincipal)
        {
            Normalizar(p);
            if (string.IsNullOrWhiteSpace(p.RazonSocial))
            {
                TempData["Error"] = "La razón social es obligatoria.";
                return RedirectToAction(nameof(Index));
            }
            if (!string.IsNullOrEmpty(p.Ruc) && (p.Ruc.Length != 11 || !p.Ruc.All(char.IsDigit)))
            {
                TempData["Error"] = "El RUC debe contener exactamente 11 dígitos.";
                return RedirectToAction(nameof(Index));
            }
            if (p.ProbabilidadEstimada is < 0 or > 100)
            {
                TempData["Error"] = "La probabilidad debe estar entre 0 y 100.";
                return RedirectToAction(nameof(Index));
            }
            int n = contactoNombre?.Count ?? 0, principal = (contactoPrincipal != null && contactoPrincipal.Count > 0) ? contactoPrincipal[0] : 0;
            if (n > 0 && principal >= n)
                principal = 0;
            if (n > 0)
            {
                p.NombreContacto = Limpiar(contactoNombre![principal]);
                p.CargoContacto = At(contactoCargo, principal);
                p.Telefono = At(contactoTelefono, principal);
                p.Correo = At(contactoCorreo, principal);
            }
            try
            {
                await using var cn = CrearConexion();
                await cn.OpenAsync();
                int id;
                await using (var c = SP(cn, "dbo.sp_Prospecto_Registrar"))
                {
                    AddProspectoParams(c, p);
                    await using var r = await c.ExecuteReaderAsync();
                    await r.ReadAsync();
                    if (!Convert.ToBoolean(r["Ok"]))
                    {
                        TempData["Error"] = r["Mensaje"].ToString();
                        return RedirectToAction(nameof(Index));
                    }
                    id = Convert.ToInt32(r["IdProspecto"]);
                }
                for (int i = 0; i < n; i++)
                {
                    var nom = Limpiar(contactoNombre![i]);
                    if (string.IsNullOrEmpty(nom))
                        continue;
                    await using var cc = SP(cn, "dbo.sp_ContactoProspecto_Registrar");
                    P(cc, "@IdProspecto", SqlDbType.Int, id);
                    P(cc, "@Nombre", SqlDbType.NVarChar, nom, 150);
                    P(cc, "@Cargo", SqlDbType.NVarChar, At(contactoCargo, i), 100);
                    P(cc, "@Telefono", SqlDbType.VarChar, At(contactoTelefono, i), 20);
                    P(cc, "@Correo", SqlDbType.NVarChar, At(contactoCorreo, i), 150);
                    P(cc, "@EsPrincipal", SqlDbType.Bit, i == principal);
                    await cc.ExecuteNonQueryAsync();
                }
                TempData["Success"] = "Prospecto registrado correctamente.";
            }
            catch (Exception ex) { TempData["Error"] = "No se pudo registrar el prospecto: " + ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Prospecto p)
        {
            Normalizar(p);
            if (p.IdProspecto <= 0 || string.IsNullOrWhiteSpace(p.RazonSocial))
            {
                TempData["Error"] = "Datos del prospecto no válidos.";
                return RedirectToAction(nameof(Index));
            }
            if (!string.IsNullOrEmpty(p.Ruc) && (p.Ruc.Length != 11 || !p.Ruc.All(char.IsDigit)))
            {
                TempData["Error"] = "El RUC debe contener exactamente 11 dígitos.";
                return RedirectToAction(nameof(Index));
            }
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using var c = SP(cn, "dbo.sp_Prospecto_Editar");
            P(c, "@IdProspecto", SqlDbType.Int, p.IdProspecto);
            P(c, "@RazonSocial", SqlDbType.NVarChar, p.RazonSocial, 200);
            P(c, "@Ruc", SqlDbType.VarChar, p.Ruc, 11);
            P(c, "@NombreComercial", SqlDbType.NVarChar, p.NombreComercial, 200);
            P(c, "@NombreContacto", SqlDbType.NVarChar, p.NombreContacto, 150);
            P(c, "@CargoContacto", SqlDbType.NVarChar, p.CargoContacto, 100);
            P(c, "@Telefono", SqlDbType.VarChar, p.Telefono, 20);
            P(c, "@Correo", SqlDbType.NVarChar, p.Correo, 150);
            P(c, "@Direccion", SqlDbType.NVarChar, p.Direccion, 300);
            P(c, "@Origen", SqlDbType.NVarChar, p.Origen, 100);
            P(c, "@Observacion", SqlDbType.NVarChar, p.Observacion, 1000);
            P(c, "@IdEstadoProspecto", SqlDbType.Int, p.IdEstadoProspecto ?? 1);
            await using var r = await c.ExecuteReaderAsync();
            await r.ReadAsync();
            if (Convert.ToBoolean(r["Ok"]))
                TempData["Success"] = r["Mensaje"].ToString();
            else
                TempData["Error"] = r["Mensaje"].ToString();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> VerificarRuc(string ruc, int? idProspecto = null)
        {
            ruc = ruc?.Trim() ?? "";
            if (ruc.Length != 11 || !ruc.All(char.IsDigit))
                return Json(new
                {
                    existe = false,
                    valido = false
                });
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using var c = SP(cn, "dbo.sp_Prospecto_VerificarRuc");
            P(c, "@Ruc", SqlDbType.VarChar, ruc, 11);
            P(c, "@ExcluirId", SqlDbType.Int, idProspecto);
            return Json(new
            {
                existe = Convert.ToBoolean(await c.ExecuteScalarAsync()),
                valido = true
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int idProspecto, int idEstado)
        {
            if (idProspecto <= 0 || (idEstado != 1 && idEstado != 2))
                return Json(new
                {
                    ok = false,
                    mensaje = "Datos no válidos."
                });
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using var c = SP(cn, "dbo.sp_Prospecto_CambiarEstado");
            P(c, "@IdProspecto", SqlDbType.Int, idProspecto);
            P(c, "@IdEstado", SqlDbType.Int, idEstado);
            await using var r = await c.ExecuteReaderAsync();
            await r.ReadAsync();
            return Json(new
            {
                ok = Convert.ToBoolean(r["Ok"]),
                mensaje = r["Mensaje"].ToString()
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarSeguimiento(int idProspecto, int idTipoGestion, DateTime fechaGestion, string? comentario, DateTime? proximaGestion, int idEstadoProspecto)
        {
            comentario = Limpiar(comentario);
            if (idProspecto <= 0 || idTipoGestion < 1 || idTipoGestion > 7 || idEstadoProspecto < 1 || idEstadoProspecto > 5)
            {
                TempData["Error"] = "Datos de seguimiento no válidos.";
                return RedirectToAction(nameof(Index));
            }
            if (proximaGestion.HasValue && proximaGestion.Value < fechaGestion)
            {
                TempData["Error"] = "La próxima gestión no puede ser anterior a la gestión realizada.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                await using var cn = CrearConexion();
                await cn.OpenAsync();
                await using var c = SP(cn, "dbo.sp_Prospecto_RegistrarSeguimiento");
                P(c, "@IdProspecto", SqlDbType.Int, idProspecto);
                P(c, "@IdTipoGestion", SqlDbType.Int, idTipoGestion);
                P(c, "@FechaGestion", SqlDbType.DateTime2, fechaGestion);
                P(c, "@Comentario", SqlDbType.NVarChar, comentario, 1000);
                P(c, "@ProximaGestion", SqlDbType.DateTime2, proximaGestion);
                P(c, "@IdEstadoProspecto", SqlDbType.Int, idEstadoProspecto);
                await c.ExecuteNonQueryAsync();
                TempData["Success"] = "Seguimiento registrado correctamente.";
            }
            catch { TempData["Error"] = "No se pudo registrar el seguimiento."; }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> HistorialSeguimientos(int idProspecto)
        {
            var x = new List<object>();
            await using var cn = CrearConexion();
            await cn.OpenAsync();
            await using var c = SP(cn, "dbo.sp_Prospecto_HistorialSeguimientos");
            P(c, "@IdProspecto", SqlDbType.Int, idProspecto);
            await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync())
                x.Add(new
                {
                    idSeguimiento = Convert.ToInt32(r["IdSeguimiento"]),
                    tipoGestion = S(r, "TipoGestion"),
                    fechaGestion = Convert.ToDateTime(r["FechaGestion"]).ToString("dd/MM/yyyy HH:mm"),
                    comentario = S(r, "Comentario"),
                    proximaGestion = NDt(r, "ProximaGestion")?.ToString("dd/MM/yyyy HH:mm"),
                    estadoProspecto = S(r, "EstadoProspecto")
                });
            return Json(x);
        }

        private static SqlCommand SP(SqlConnection cn, string nombre)
        {
            var c = new SqlCommand(nombre, cn);
            c.CommandType = CommandType.StoredProcedure;
            return c;
        }
        private static void P(SqlCommand c, string n, SqlDbType t, object? v, int size = 0)
        {
            var p = size > 0 ? c.Parameters.Add(n, t, size) : c.Parameters.Add(n, t);
            p.Value = v is string s ? (string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim()) : v ?? DBNull.Value;
        }
        private static void AddProspectoParams(SqlCommand c, Prospecto p)
        {
            P(c, "@RazonSocial", SqlDbType.NVarChar, p.RazonSocial, 200);
            P(c, "@Ruc", SqlDbType.VarChar, p.Ruc, 11);
            P(c, "@NombreComercial", SqlDbType.NVarChar, p.NombreComercial, 200);
            P(c, "@NombreContacto", SqlDbType.NVarChar, p.NombreContacto, 150);
            P(c, "@CargoContacto", SqlDbType.NVarChar, p.CargoContacto, 100);
            P(c, "@Telefono", SqlDbType.VarChar, p.Telefono, 20);
            P(c, "@Correo", SqlDbType.NVarChar, p.Correo, 150);
            P(c, "@Direccion", SqlDbType.NVarChar, p.Direccion, 300);
            P(c, "@Origen", SqlDbType.NVarChar, p.Origen, 100);
            P(c, "@Observacion", SqlDbType.NVarChar, p.Observacion, 1000);
            P(c, "@TipoPersona", SqlDbType.NVarChar, p.TipoPersona, 30);
            P(c, "@IdSector", SqlDbType.Int, p.IdSector);
            P(c, "@IdActividad", SqlDbType.Int, p.IdActividad);
            P(c, "@PaginaWeb", SqlDbType.NVarChar, p.PaginaWeb, 250);
            P(c, "@IdDepartamento", SqlDbType.Int, p.IdDepartamento);
            P(c, "@IdProvincia", SqlDbType.Int, p.IdProvincia);
            P(c, "@IdDistrito", SqlDbType.Int, p.IdDistrito);
            P(c, "@Fuente", SqlDbType.NVarChar, p.Fuente, 150);
            P(c, "@Prioridad", SqlDbType.NVarChar, p.Prioridad, 20);
            P(c, "@TamanoEstimado", SqlDbType.NVarChar, p.TamanoEstimado, 30);
            var pr = c.Parameters.Add("@ProbabilidadEstimada", SqlDbType.Decimal);
            pr.Precision = 5;
            pr.Scale = 2;
            pr.Value = (object?)p.ProbabilidadEstimada ?? DBNull.Value;
            P(c, "@TipoCliente", SqlDbType.NVarChar, p.TipoCliente, 30);
            P(c, "@CompetenciaActual", SqlDbType.NVarChar, p.CompetenciaActual, 200);
            P(c, "@FechaEstimadaInicio", SqlDbType.Date, p.FechaEstimadaInicio);
            P(c, "@ProximaGestion", SqlDbType.DateTime2, p.ProximaGestion);
        }
        private static Prospecto Map(SqlDataReader r) => new() { IdProspecto = Convert.ToInt32(r["IdProspecto"]), RazonSocial = S(r, "RazonSocial") ?? "", Ruc = S(r, "Ruc"), NombreComercial = S(r, "NombreComercial"), NombreContacto = S(r, "NombreContacto"), CargoContacto = S(r, "CargoContacto"), Telefono = S(r, "Telefono"), Correo = S(r, "Correo"), Direccion = S(r, "Direccion"), Origen = S(r, "Origen"), Observacion = S(r, "Observacion"), IdUsuarioResponsable = NI(r, "IdUsuarioResponsable"), IdEstado = Convert.ToInt32(r["IdEstado"]), NombreEstado = S(r, "NombreEstado"), IdEstadoProspecto = NI(r, "IdEstadoProspecto"), NombreEstadoProspecto = S(r, "NombreEstadoProspecto"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), TipoPersona = S(r, "TipoPersona"), IdSector = NI(r, "IdSector"), IdActividad = NI(r, "IdActividad"), PaginaWeb = S(r, "PaginaWeb"), IdDepartamento = NI(r, "IdDepartamento"), IdProvincia = NI(r, "IdProvincia"), IdDistrito = NI(r, "IdDistrito"), Fuente = S(r, "Fuente"), Prioridad = S(r, "Prioridad"), TamanoEstimado = S(r, "TamanoEstimado"), ProbabilidadEstimada = ND(r, "ProbabilidadEstimada"), TipoCliente = S(r, "TipoCliente"), CompetenciaActual = S(r, "CompetenciaActual"), FechaEstimadaInicio = NDt(r, "FechaEstimadaInicio"), ProximaGestion = NDt(r, "ProximaGestionFinal") };
        private static void Normalizar(Prospecto p)
        {
            p.RazonSocial = p.RazonSocial?.Trim() ?? "";
            p.Ruc = Limpiar(p.Ruc);
            p.NombreComercial = Limpiar(p.NombreComercial);
            p.Direccion = Limpiar(p.Direccion);
            p.Origen = Limpiar(p.Origen);
            p.Observacion = Limpiar(p.Observacion);
            p.TipoPersona = Limpiar(p.TipoPersona);
            p.PaginaWeb = Limpiar(p.PaginaWeb);
            p.Fuente = Limpiar(p.Fuente);
            p.Prioridad = Limpiar(p.Prioridad);
            p.TamanoEstimado = Limpiar(p.TamanoEstimado);
            p.TipoCliente = Limpiar(p.TipoCliente);
            p.CompetenciaActual = Limpiar(p.CompetenciaActual);
        }
        private static string? At(List<string>? x, int i) => x != null && i < x.Count ? Limpiar(x[i]) : null; private static string? Limpiar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim(); private static string? S(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : r[n].ToString(); private static int? NI(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : Convert.ToInt32(r[n]); private static decimal? ND(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : Convert.ToDecimal(r[n]); private static DateTime? NDt(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : Convert.ToDateTime(r[n]);
    }
}