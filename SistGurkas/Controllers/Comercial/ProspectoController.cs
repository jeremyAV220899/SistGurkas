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

        private SqlConnection CrearConexion()
        {
            var cadena = _configuration.GetConnectionString("SistGurkas")
                ?? throw new InvalidOperationException("No se encontró la conexión SistGurkas.");
            return new SqlConnection(cadena);
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, int? idEstadoProspecto,
            string? origen, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            buscar = Limpiar(buscar); origen = Limpiar(origen);
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date)
            {
                TempData["Error"] = "La fecha desde no puede ser mayor que la fecha hasta.";
                return RedirectToAction(nameof(Index));
            }

            var prospectos = new List<Prospecto>();
            const string sql = @"
SELECT p.*, e.NombreEstado, ep.Nombre NombreEstadoProspecto,
       COALESCE(p.ProximaGestion, sg.ProximaGestion) ProximaGestionFinal
FROM dbo.Prospecto p
INNER JOIN dbo.Estado e ON e.IdEstado=p.IdEstado
LEFT JOIN dbo.EstadoProspecto ep ON ep.IdEstadoProspecto=p.IdEstadoProspecto
OUTER APPLY (
 SELECT TOP 1 sp.ProximaGestion FROM dbo.SeguimientoProspecto sp
 WHERE sp.IdProspecto=p.IdProspecto AND sp.IdEstado=1 AND sp.ProximaGestion IS NOT NULL
 ORDER BY sp.FechaRegistro DESC, sp.IdSeguimiento DESC
) sg
WHERE (@Buscar IS NULL OR p.RazonSocial LIKE '%'+@Buscar+'%' OR p.NombreComercial LIKE '%'+@Buscar+'%'
 OR p.Ruc LIKE '%'+@Buscar+'%' OR p.NombreContacto LIKE '%'+@Buscar+'%' OR p.Telefono LIKE '%'+@Buscar+'%')
AND (@IdEstadoProspecto IS NULL OR p.IdEstadoProspecto=@IdEstadoProspecto)
AND (@Origen IS NULL OR p.Origen=@Origen)
AND (@FechaDesde IS NULL OR p.FechaRegistro>=@FechaDesde)
AND (@FechaHastaExclusiva IS NULL OR p.FechaRegistro<@FechaHastaExclusiva)
ORDER BY p.IdProspecto DESC;";

            await using var cn = CrearConexion(); await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.Add("@Buscar", SqlDbType.NVarChar, 200).Value = DbValor(buscar);
            cmd.Parameters.Add("@IdEstadoProspecto", SqlDbType.Int).Value = (object?)idEstadoProspecto ?? DBNull.Value;
            cmd.Parameters.Add("@Origen", SqlDbType.NVarChar, 100).Value = DbValor(origen);
            cmd.Parameters.Add("@FechaDesde", SqlDbType.DateTime2).Value = fechaDesde.HasValue ? fechaDesde.Value.Date : DBNull.Value;
            cmd.Parameters.Add("@FechaHastaExclusiva", SqlDbType.DateTime2).Value = fechaHasta.HasValue ? fechaHasta.Value.Date.AddDays(1) : DBNull.Value;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                prospectos.Add(new Prospecto
                {
                    IdProspecto = Convert.ToInt32(r["IdProspecto"]),
                    RazonSocial = S(r, "RazonSocial") ?? "",
                    Ruc = S(r, "Ruc"),
                    NombreComercial = S(r, "NombreComercial"),
                    NombreContacto = S(r, "NombreContacto"),
                    CargoContacto = S(r, "CargoContacto"),
                    Telefono = S(r, "Telefono"),
                    Correo = S(r, "Correo"),
                    Direccion = S(r, "Direccion"),
                    Origen = S(r, "Origen"),
                    Observacion = S(r, "Observacion"),
                    IdUsuarioResponsable = NI(r, "IdUsuarioResponsable"),
                    IdEstado = Convert.ToInt32(r["IdEstado"]),
                    NombreEstado = S(r, "NombreEstado"),
                    IdEstadoProspecto = NI(r, "IdEstadoProspecto"),
                    NombreEstadoProspecto = S(r, "NombreEstadoProspecto"),
                    FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]),
                    TipoPersona = S(r, "TipoPersona"),
                    IdSector = NI(r, "IdSector"),
                    IdActividad = NI(r, "IdActividad"),
                    PaginaWeb = S(r, "PaginaWeb"),
                    IdDepartamento = NI(r, "IdDepartamento"),
                    IdProvincia = NI(r, "IdProvincia"),
                    IdDistrito = NI(r, "IdDistrito"),
                    Fuente = S(r, "Fuente"),
                    Prioridad = S(r, "Prioridad"),
                    TamanoEstimado = S(r, "TamanoEstimado"),
                    ProbabilidadEstimada = ND(r, "ProbabilidadEstimada"),
                    TipoCliente = S(r, "TipoCliente"),
                    CompetenciaActual = S(r, "CompetenciaActual"),
                    FechaEstimadaInicio = NDt(r, "FechaEstimadaInicio"),
                    ProximaGestion = NDt(r, "ProximaGestionFinal")
                });
            }
            ViewBag.Buscar = buscar; ViewBag.IdEstadoProspecto = idEstadoProspecto; ViewBag.Origen = origen;
            ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd"); ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");
            await CargarCatalogosAsync();
            return View("~/Views/Comercial/Prospecto/Index.cshtml", prospectos);
        }

        private async Task CargarCatalogosAsync()
        {
            await using var cn = CrearConexion(); await cn.OpenAsync();
            ViewBag.Sectores = await CatalogoAsync(cn, "SELECT IdSector Id, Nombre FROM dbo.SectorProspecto WHERE IdEstado=1 ORDER BY Nombre");
            ViewBag.Actividades = await CatalogoAsync(cn, "SELECT IdActividad Id, Nombre FROM dbo.ActividadProspecto WHERE IdEstado=1 ORDER BY Nombre");
            ViewBag.Departamentos = await CatalogoAsync(cn, "SELECT IdDepartamento Id, Nombre FROM dbo.Departamento WHERE IdEstado=1 ORDER BY Nombre");
        }
        private static async Task<List<dynamic>> CatalogoAsync(SqlConnection cn, string sql)
        {
            var x = new List<dynamic>(); await using var c = new SqlCommand(sql, cn); await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync()) x.Add(new { Id = Convert.ToInt32(r["Id"]), Nombre = r["Nombre"].ToString() ?? "" });
            return x;
        }

        [HttpGet]
        public async Task<IActionResult> Provincias(int idDepartamento)
        {
            var x = new List<object>(); await using var cn = CrearConexion(); await cn.OpenAsync();
            await using var c = new SqlCommand("SELECT IdProvincia,Nombre FROM dbo.Provincia WHERE IdDepartamento=@Id AND IdEstado=1 ORDER BY Nombre", cn);
            c.Parameters.Add("@Id", SqlDbType.Int).Value = idDepartamento; await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync()) x.Add(new { id = Convert.ToInt32(r["IdProvincia"]), nombre = r["Nombre"].ToString() });
            return Json(x);
        }
        [HttpGet]
        public async Task<IActionResult> Distritos(int idProvincia)
        {
            var x = new List<object>(); await using var cn = CrearConexion(); await cn.OpenAsync();
            await using var c = new SqlCommand("SELECT IdDistrito,Nombre,CodigoUbigeo FROM dbo.Distrito WHERE IdProvincia=@Id AND IdEstado=1 ORDER BY Nombre", cn);
            c.Parameters.Add("@Id", SqlDbType.Int).Value = idProvincia; await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync()) x.Add(new { id = Convert.ToInt32(r["IdDistrito"]), nombre = r["Nombre"].ToString(), ubigeo = r["CodigoUbigeo"].ToString() });
            return Json(x);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Prospecto prospecto,
            List<string>? contactoNombre, List<string>? contactoCargo, List<string>? contactoTelefono,
            List<string>? contactoCorreo, List<int>? contactoPrincipal)
        {
            Normalizar(prospecto);
            if (string.IsNullOrWhiteSpace(prospecto.RazonSocial)) { TempData["Error"] = "La razón social es obligatoria."; return RedirectToAction(nameof(Index)); }
            if (!string.IsNullOrEmpty(prospecto.Ruc) && (prospecto.Ruc.Length != 11 || !prospecto.Ruc.All(char.IsDigit))) { TempData["Error"] = "El RUC debe contener exactamente 11 dígitos."; return RedirectToAction(nameof(Index)); }
            if (!string.IsNullOrEmpty(prospecto.Ruc) && await ExisteRucAsync(prospecto.Ruc)) { TempData["Error"] = "Ya existe un prospecto registrado con ese RUC."; return RedirectToAction(nameof(Index)); }
            if (prospecto.ProbabilidadEstimada is < 0 or > 100) { TempData["Error"] = "La probabilidad debe estar entre 0 y 100."; return RedirectToAction(nameof(Index)); }

            await using var cn = CrearConexion(); await cn.OpenAsync(); await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                const string sql = @"
INSERT dbo.Prospecto(RazonSocial,Ruc,NombreComercial,NombreContacto,CargoContacto,Telefono,Correo,Direccion,Origen,Observacion,
IdUsuarioResponsable,IdEstado,IdEstadoProspecto,TipoPersona,IdSector,IdActividad,PaginaWeb,IdDepartamento,IdProvincia,IdDistrito,
Fuente,Prioridad,TamanoEstimado,ProbabilidadEstimada,TipoCliente,CompetenciaActual,FechaEstimadaInicio,ProximaGestion)
OUTPUT INSERTED.IdProspecto
VALUES(@RazonSocial,@Ruc,@NombreComercial,@NombreContacto,@CargoContacto,@Telefono,@Correo,@Direccion,@Origen,@Observacion,
NULL,1,1,@TipoPersona,@IdSector,@IdActividad,@PaginaWeb,@IdDepartamento,@IdProvincia,@IdDistrito,@Fuente,@Prioridad,@Tamano,
@Probabilidad,@TipoCliente,@Competencia,@FechaInicio,@ProximaGestion);";
                string? principalNombre = null, principalCargo = null, principalTel = null, principalCorreo = null;
                int n = contactoNombre?.Count ?? 0; int principal = (contactoPrincipal != null && contactoPrincipal.Count > 0) ? contactoPrincipal[0] : 0;
                if (n > 0 && principal >= n) principal = 0;
                if (n > 0) { principalNombre = Limpiar(contactoNombre![principal]); principalCargo = At(contactoCargo, principal); principalTel = At(contactoTelefono, principal); principalCorreo = At(contactoCorreo, principal); }
                prospecto.NombreContacto = principalNombre; prospecto.CargoContacto = principalCargo; prospecto.Telefono = principalTel; prospecto.Correo = principalCorreo;

                await using var c = new SqlCommand(sql, cn, tx); AddProspectoParams(c, prospecto);
                int id = Convert.ToInt32(await c.ExecuteScalarAsync());

                for (int i = 0; i < n; i++)
                {
                    var nom = Limpiar(contactoNombre![i]); if (string.IsNullOrEmpty(nom)) continue;
                    await using var cc = new SqlCommand(@"INSERT dbo.ContactoProspecto(IdProspecto,Nombre,Cargo,Telefono,Correo,EsPrincipal,Observacion,IdEstado)
VALUES(@P,@N,@C,@T,@E,@Principal,NULL,1);", cn, tx);
                    cc.Parameters.Add("@P", SqlDbType.Int).Value = id; cc.Parameters.Add("@N", SqlDbType.NVarChar, 150).Value = nom;
                    cc.Parameters.Add("@C", SqlDbType.NVarChar, 100).Value = DbValor(At(contactoCargo, i));
                    cc.Parameters.Add("@T", SqlDbType.VarChar, 20).Value = DbValor(At(contactoTelefono, i));
                    cc.Parameters.Add("@E", SqlDbType.NVarChar, 150).Value = DbValor(At(contactoCorreo, i));
                    cc.Parameters.Add("@Principal", SqlDbType.Bit).Value = i == principal;
                    await cc.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync(); TempData["Success"] = "Prospecto registrado correctamente.";
            }
            catch (Exception ex) { await tx.RollbackAsync(); System.Diagnostics.Debug.WriteLine("ERROR REGISTRO PROSPECTO: " + ex); TempData["Error"] = "No se pudo registrar el prospecto: " + ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        private static void AddProspectoParams(SqlCommand c, Prospecto p)
        {
            c.Parameters.Add("@RazonSocial", SqlDbType.NVarChar, 200).Value = p.RazonSocial;
            c.Parameters.Add("@Ruc", SqlDbType.VarChar, 11).Value = DbValor(p.Ruc); c.Parameters.Add("@NombreComercial", SqlDbType.NVarChar, 200).Value = DbValor(p.NombreComercial);
            c.Parameters.Add("@NombreContacto", SqlDbType.NVarChar, 150).Value = DbValor(p.NombreContacto); c.Parameters.Add("@CargoContacto", SqlDbType.NVarChar, 100).Value = DbValor(p.CargoContacto);
            c.Parameters.Add("@Telefono", SqlDbType.VarChar, 20).Value = DbValor(p.Telefono); c.Parameters.Add("@Correo", SqlDbType.NVarChar, 150).Value = DbValor(p.Correo);
            c.Parameters.Add("@Direccion", SqlDbType.NVarChar, 300).Value = DbValor(p.Direccion); c.Parameters.Add("@Origen", SqlDbType.NVarChar, 100).Value = DbValor(p.Origen);
            c.Parameters.Add("@Observacion", SqlDbType.NVarChar, 1000).Value = DbValor(p.Observacion); c.Parameters.Add("@TipoPersona", SqlDbType.NVarChar, 30).Value = DbValor(p.TipoPersona);
            c.Parameters.Add("@IdSector", SqlDbType.Int).Value = (object?)p.IdSector ?? DBNull.Value; c.Parameters.Add("@IdActividad", SqlDbType.Int).Value = (object?)p.IdActividad ?? DBNull.Value;
            c.Parameters.Add("@PaginaWeb", SqlDbType.NVarChar, 250).Value = DbValor(p.PaginaWeb); c.Parameters.Add("@IdDepartamento", SqlDbType.Int).Value = (object?)p.IdDepartamento ?? DBNull.Value;
            c.Parameters.Add("@IdProvincia", SqlDbType.Int).Value = (object?)p.IdProvincia ?? DBNull.Value; c.Parameters.Add("@IdDistrito", SqlDbType.Int).Value = (object?)p.IdDistrito ?? DBNull.Value;
            c.Parameters.Add("@Fuente", SqlDbType.NVarChar, 150).Value = DbValor(p.Fuente); c.Parameters.Add("@Prioridad", SqlDbType.NVarChar, 20).Value = DbValor(p.Prioridad);
            c.Parameters.Add("@Tamano", SqlDbType.NVarChar, 30).Value = DbValor(p.TamanoEstimado); c.Parameters.Add("@Probabilidad", SqlDbType.Decimal).Value = (object?)p.ProbabilidadEstimada ?? DBNull.Value;
            c.Parameters["@Probabilidad"].Precision = 5; c.Parameters["@Probabilidad"].Scale = 2;
            c.Parameters.Add("@TipoCliente", SqlDbType.NVarChar, 30).Value = DbValor(p.TipoCliente); c.Parameters.Add("@Competencia", SqlDbType.NVarChar, 200).Value = DbValor(p.CompetenciaActual);
            c.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = (object?)p.FechaEstimadaInicio ?? DBNull.Value; c.Parameters.Add("@ProximaGestion", SqlDbType.DateTime2).Value = (object?)p.ProximaGestion ?? DBNull.Value;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Prospecto p)
        {
            Normalizar(p);
            if (p.IdProspecto <= 0 || string.IsNullOrWhiteSpace(p.RazonSocial)) { TempData["Error"] = "Datos del prospecto no válidos."; return RedirectToAction(nameof(Index)); }
            if (!string.IsNullOrEmpty(p.Ruc) && (p.Ruc.Length != 11 || !p.Ruc.All(char.IsDigit))) { TempData["Error"] = "El RUC debe contener exactamente 11 dígitos."; return RedirectToAction(nameof(Index)); }
            if (!string.IsNullOrEmpty(p.Ruc) && await ExisteRucAsync(p.Ruc, p.IdProspecto)) { TempData["Error"] = "El RUC pertenece a otro prospecto."; return RedirectToAction(nameof(Index)); }
            const string sql = @"UPDATE dbo.Prospecto SET RazonSocial=@RazonSocial,Ruc=@Ruc,NombreComercial=@NombreComercial,
NombreContacto=@NombreContacto,CargoContacto=@CargoContacto,Telefono=@Telefono,Correo=@Correo,Direccion=@Direccion,Origen=@Origen,
Observacion=@Observacion,IdEstadoProspecto=@Estado WHERE IdProspecto=@Id;";
            await using var cn = CrearConexion(); await cn.OpenAsync(); await using var c = new SqlCommand(sql, cn);
            c.Parameters.AddWithValue("@Id", p.IdProspecto); c.Parameters.AddWithValue("@RazonSocial", p.RazonSocial); c.Parameters.AddWithValue("@Ruc", DbValor(p.Ruc));
            c.Parameters.AddWithValue("@NombreComercial", DbValor(p.NombreComercial)); c.Parameters.AddWithValue("@NombreContacto", DbValor(p.NombreContacto));
            c.Parameters.AddWithValue("@CargoContacto", DbValor(p.CargoContacto)); c.Parameters.AddWithValue("@Telefono", DbValor(p.Telefono)); c.Parameters.AddWithValue("@Correo", DbValor(p.Correo));
            c.Parameters.AddWithValue("@Direccion", DbValor(p.Direccion)); c.Parameters.AddWithValue("@Origen", DbValor(p.Origen)); c.Parameters.AddWithValue("@Observacion", DbValor(p.Observacion));
            c.Parameters.AddWithValue("@Estado", p.IdEstadoProspecto ?? 1); await c.ExecuteNonQueryAsync(); TempData["Success"] = "Prospecto actualizado correctamente."; return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> VerificarRuc(string ruc, int? idProspecto = null)
        {
            ruc = ruc?.Trim() ?? ""; if (ruc.Length != 11 || !ruc.All(char.IsDigit)) return Json(new { existe = false, valido = false });
            return Json(new { existe = await ExisteRucAsync(ruc, idProspecto), valido = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int idProspecto, int idEstado)
        {
            if (idProspecto <= 0 || (idEstado != 1 && idEstado != 2)) return Json(new { ok = false, mensaje = "Datos no válidos." });
            await using var cn = CrearConexion(); await cn.OpenAsync(); await using var c = new SqlCommand("UPDATE dbo.Prospecto SET IdEstado=@E WHERE IdProspecto=@I", cn);
            c.Parameters.AddWithValue("@E", idEstado); c.Parameters.AddWithValue("@I", idProspecto); int n = await c.ExecuteNonQueryAsync();
            return Json(new { ok = n > 0, mensaje = n > 0 ? (idEstado == 1 ? "Prospecto activado correctamente." : "Prospecto dado de baja correctamente.") : "No se encontró el prospecto." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarSeguimiento(int idProspecto, int idTipoGestion, DateTime fechaGestion, string? comentario, DateTime? proximaGestion, int idEstadoProspecto)
        {
            comentario = Limpiar(comentario);
            if (idProspecto <= 0 || idTipoGestion < 1 || idTipoGestion > 7 || idEstadoProspecto < 1 || idEstadoProspecto > 5) { TempData["Error"] = "Datos de seguimiento no válidos."; return RedirectToAction(nameof(Index)); }
            if (proximaGestion.HasValue && proximaGestion.Value < fechaGestion) { TempData["Error"] = "La próxima gestión no puede ser anterior a la gestión realizada."; return RedirectToAction(nameof(Index)); }
            const string sql = @"INSERT dbo.SeguimientoProspecto(IdProspecto,IdTipoGestion,FechaGestion,Comentario,ProximaGestion,IdEstadoProspecto,IdUsuarioRegistro,IdEstado)
VALUES(@P,@T,@F,@C,@PG,@EP,NULL,1);
UPDATE dbo.Prospecto SET IdEstadoProspecto=@EP,ProximaGestion=@PG WHERE IdProspecto=@P;";
            await using var cn = CrearConexion(); await cn.OpenAsync(); await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                await using var c = new SqlCommand(sql, cn, tx); c.Parameters.AddWithValue("@P", idProspecto); c.Parameters.AddWithValue("@T", idTipoGestion); c.Parameters.AddWithValue("@F", fechaGestion);
                c.Parameters.AddWithValue("@C", DbValor(comentario)); c.Parameters.Add("@PG", SqlDbType.DateTime2).Value = (object?)proximaGestion ?? DBNull.Value; c.Parameters.AddWithValue("@EP", idEstadoProspecto);
                await c.ExecuteNonQueryAsync(); await tx.CommitAsync(); TempData["Success"] = "Seguimiento registrado correctamente.";
            }
            catch { await tx.RollbackAsync(); TempData["Error"] = "No se pudo registrar el seguimiento."; }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> HistorialSeguimientos(int idProspecto)
        {
            const string sql = @"SELECT sp.IdSeguimiento,tg.Nombre TipoGestion,sp.FechaGestion,sp.Comentario,sp.ProximaGestion,ep.Nombre EstadoProspecto
FROM dbo.SeguimientoProspecto sp INNER JOIN dbo.TipoGestionProspecto tg ON tg.IdTipoGestion=sp.IdTipoGestion
INNER JOIN dbo.EstadoProspecto ep ON ep.IdEstadoProspecto=sp.IdEstadoProspecto WHERE sp.IdProspecto=@P AND sp.IdEstado=1 ORDER BY sp.FechaGestion DESC,sp.IdSeguimiento DESC;";
            var x = new List<object>(); await using var cn = CrearConexion(); await cn.OpenAsync(); await using var c = new SqlCommand(sql, cn); c.Parameters.AddWithValue("@P", idProspecto); await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync()) x.Add(new
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

        private async Task<bool> ExisteRucAsync(string ruc, int? excluirId = null)
        {
            var sql = "SELECT COUNT(1) FROM dbo.Prospecto WHERE Ruc=@Ruc" + (excluirId.HasValue ? " AND IdProspecto<>@Id" : "");
            await using var cn = CrearConexion(); await cn.OpenAsync(); await using var c = new SqlCommand(sql, cn); c.Parameters.AddWithValue("@Ruc", ruc); if (excluirId.HasValue) c.Parameters.AddWithValue("@Id", excluirId.Value);
            return Convert.ToInt32(await c.ExecuteScalarAsync()) > 0;
        }
        private static void Normalizar(Prospecto p) { p.RazonSocial = p.RazonSocial?.Trim() ?? ""; p.Ruc = Limpiar(p.Ruc); p.NombreComercial = Limpiar(p.NombreComercial); p.Direccion = Limpiar(p.Direccion); p.Origen = Limpiar(p.Origen); p.Observacion = Limpiar(p.Observacion); p.TipoPersona = Limpiar(p.TipoPersona); p.PaginaWeb = Limpiar(p.PaginaWeb); p.Fuente = Limpiar(p.Fuente); p.Prioridad = Limpiar(p.Prioridad); p.TamanoEstimado = Limpiar(p.TamanoEstimado); p.TipoCliente = Limpiar(p.TipoCliente); p.CompetenciaActual = Limpiar(p.CompetenciaActual); }
        private static string? At(List<string>? x, int i) => x != null && i < x.Count ? Limpiar(x[i]) : null;
        private static string? Limpiar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        private static object DbValor(string? v) => string.IsNullOrWhiteSpace(v) ? DBNull.Value : v;
        private static string? S(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : r[n].ToString();
        private static int? NI(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : Convert.ToInt32(r[n]);
        private static decimal? ND(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : Convert.ToDecimal(r[n]);
        private static DateTime? NDt(SqlDataReader r, string n) => r[n] == DBNull.Value ? null : Convert.ToDateTime(r[n]);
    }
}
