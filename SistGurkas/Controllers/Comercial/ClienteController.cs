using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistGurkas.Models.Comercial;
using System.Data;

namespace SistGurkas.Controllers.Comercial
{
    public class ClienteController : Controller
    {
        private readonly IConfiguration _configuration;
        public ClienteController(IConfiguration configuration) => _configuration = configuration;
        private SqlConnection Conexion() => new(_configuration.GetConnectionString("SistGurkas") ?? throw new InvalidOperationException("Falta la conexión SistGurkas."));

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, int? idEmpresa, int? idEstado)
        {
            var lista = new List<Unidad>(); var empresas = new List<(int Id, string Nombre)>();
            await using var cn = Conexion(); await cn.OpenAsync();
            const string sql = @"SELECT u.*,e.NombreEmpresa EmpresaNombre FROM dbo.Unidad u INNER JOIN dbo.Empresa e ON e.IdEmpresa=u.IdEmpresa
 WHERE (@B IS NULL OR u.CodigoUnidad LIKE '%'+@B+'%' OR u.RazonSocial LIKE '%'+@B+'%' OR u.RucUnidad LIKE '%'+@B+'%' OR u.NombreComercial LIKE '%'+@B+'%')
 AND (@E IS NULL OR u.IdEmpresa=@E) AND (@S IS NULL OR u.IdEstado=@S) ORDER BY u.IdUnidad DESC;";
            await using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@B", SqlDbType.NVarChar, 250).Value = string.IsNullOrWhiteSpace(buscar) ? DBNull.Value : buscar.Trim();
                cmd.Parameters.Add("@E", SqlDbType.Int).Value = (object?)idEmpresa ?? DBNull.Value; cmd.Parameters.Add("@S", SqlDbType.Int).Value = (object?)idEstado ?? DBNull.Value;
                await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) lista.Add(MapUnidad(r));
            }
            await using (var cmd = new SqlCommand("SELECT IdEmpresa,NombreEmpresa FROM dbo.Empresa WHERE IdEstado=1 ORDER BY NombreEmpresa", cn)) { await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) empresas.Add((Convert.ToInt32(r["IdEmpresa"]), r["NombreEmpresa"]?.ToString() ?? "")); }
            ViewBag.Empresas = empresas; ViewBag.Buscar = buscar; ViewBag.IdEmpresa = idEmpresa; ViewBag.IdEstado = idEstado;
            return View("~/Views/Comercial/Cliente/Index.cshtml", lista);
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            if (id <= 0) return Json(new { ok = false, mensaje = "Cliente no válido." }); var vm = new ClienteDetalleVm();
            await using var cn = Conexion(); await cn.OpenAsync();
            await using (var cmd = new SqlCommand("SELECT u.*,e.NombreEmpresa EmpresaNombre FROM dbo.Unidad u INNER JOIN dbo.Empresa e ON e.IdEmpresa=u.IdEmpresa WHERE u.IdUnidad=@I", cn)) { cmd.Parameters.Add("@I", SqlDbType.Int).Value = id; await using var r = await cmd.ExecuteReaderAsync(); if (!await r.ReadAsync()) return Json(new { ok = false, mensaje = "Cliente no encontrado." }); vm.Cliente = MapUnidad(r); }
            await using (var cmd = new SqlCommand("SELECT * FROM dbo.ContactoUnidad WHERE IdUnidad=@I ORDER BY EsPrincipal DESC,IdContactoUnidad DESC", cn)) { cmd.Parameters.Add("@I", SqlDbType.Int).Value = id; await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) vm.Contactos.Add(MapContacto(r)); }
            await using (var cmd = new SqlCommand("SELECT * FROM dbo.Sede WHERE IdUnidad=@I ORDER BY IdSede DESC", cn)) { cmd.Parameters.Add("@I", SqlDbType.Int).Value = id; await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) vm.Sedes.Add(MapSede(r)); }
            return Json(new { ok = true, data = vm });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(Unidad u)
        {
            if (string.IsNullOrWhiteSpace(u.CodigoUnidad) || u.IdEmpresa <= 0 || string.IsNullOrWhiteSpace(u.RazonSocial)) { TempData["Error"] = "Código, empresa y razón social son obligatorios."; return RedirectToAction(nameof(Index)); }
            if (!string.IsNullOrWhiteSpace(u.RucUnidad) && (u.RucUnidad.Length != 11 || !u.RucUnidad.All(char.IsDigit))) { TempData["Error"] = "El RUC debe tener 11 dígitos."; return RedirectToAction(nameof(Index)); }
            try
            {
                await using var cn = Conexion(); await cn.OpenAsync();
                const string sql = @"IF @I=0 INSERT INTO dbo.Unidad(CodigoUnidad,IdEmpresa,RazonSocial,RucUnidad,NombreComercial,IdSector,IdDepartamento,IdProvincia,IdDistrito,Direccion,Telefono,Correo,PaginaWeb,CentroCosto,Observacion,FechaActivacion,FechaBaja,IdEstado)
 VALUES(@C,@E,@R,@RU,@N,@SE,@D,@P,@DI,@A,@T,@CO,@W,@CC,@O,@FA,@FB,1)
 ELSE UPDATE dbo.Unidad SET CodigoUnidad=@C,IdEmpresa=@E,RazonSocial=@R,RucUnidad=@RU,NombreComercial=@N,IdSector=@SE,IdDepartamento=@D,IdProvincia=@P,IdDistrito=@DI,Direccion=@A,Telefono=@T,Correo=@CO,PaginaWeb=@W,CentroCosto=@CC,Observacion=@O,FechaActivacion=@FA,FechaBaja=@FB WHERE IdUnidad=@I";
                await using var cmd = new SqlCommand(sql, cn); P(cmd, "@I", SqlDbType.Int, u.IdUnidad); P(cmd, "@C", SqlDbType.VarChar, u.CodigoUnidad, 20); P(cmd, "@E", SqlDbType.Int, u.IdEmpresa); P(cmd, "@R", SqlDbType.NVarChar, u.RazonSocial, 250); P(cmd, "@RU", SqlDbType.VarChar, u.RucUnidad, 11); P(cmd, "@N", SqlDbType.NVarChar, u.NombreComercial, 150);
                P(cmd, "@SE", SqlDbType.Int, u.IdSector); P(cmd, "@D", SqlDbType.Int, u.IdDepartamento); P(cmd, "@P", SqlDbType.Int, u.IdProvincia); P(cmd, "@DI", SqlDbType.Int, u.IdDistrito); P(cmd, "@A", SqlDbType.NVarChar, u.Direccion, 300); P(cmd, "@T", SqlDbType.VarChar, u.Telefono, 20); P(cmd, "@CO", SqlDbType.NVarChar, u.Correo, 150); P(cmd, "@W", SqlDbType.NVarChar, u.PaginaWeb, 250); P(cmd, "@CC", SqlDbType.NVarChar, u.CentroCosto, 100); P(cmd, "@O", SqlDbType.NVarChar, u.Observacion, 1000); P(cmd, "@FA", SqlDbType.Date, u.FechaActivacion); P(cmd, "@FB", SqlDbType.Date, u.FechaBaja); await cmd.ExecuteNonQueryAsync(); TempData["Success"] = u.IdUnidad == 0 ? "Cliente registrado correctamente." : "Cliente actualizado correctamente.";
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) { TempData["Error"] = "El código de unidad o RUC ya está registrado."; }
            catch (Exception ex) { TempData["Error"] = "No se pudo guardar: " + ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarContacto(ContactoUnidad c)
        {
            if (c.IdUnidad <= 0 || string.IsNullOrWhiteSpace(c.Nombre)) return Json(new { ok = false, mensaje = "Cliente y nombre son obligatorios." });
            try
            {
                await using var cn = Conexion(); await cn.OpenAsync(); await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
                if (c.EsPrincipal) { await using var q = new SqlCommand("UPDATE dbo.ContactoUnidad SET EsPrincipal=0 WHERE IdUnidad=@U AND IdContactoUnidad<>@I", cn, tx); P(q, "@U", SqlDbType.Int, c.IdUnidad); P(q, "@I", SqlDbType.Int, c.IdContactoUnidad); await q.ExecuteNonQueryAsync(); }
                await using var cmd = new SqlCommand(@"IF @I=0 INSERT INTO dbo.ContactoUnidad(IdUnidad,Nombre,Cargo,Telefono,Correo,EsPrincipal,Observacion,IdEstado) VALUES(@U,@N,@C,@T,@CO,@EP,@O,1)
 ELSE UPDATE dbo.ContactoUnidad SET Nombre=@N,Cargo=@C,Telefono=@T,Correo=@CO,EsPrincipal=@EP,Observacion=@O WHERE IdContactoUnidad=@I AND IdUnidad=@U", cn, tx);
                P(cmd, "@I", SqlDbType.Int, c.IdContactoUnidad); P(cmd, "@U", SqlDbType.Int, c.IdUnidad); P(cmd, "@N", SqlDbType.NVarChar, c.Nombre, 150); P(cmd, "@C", SqlDbType.NVarChar, c.Cargo, 100); P(cmd, "@T", SqlDbType.VarChar, c.Telefono, 20); P(cmd, "@CO", SqlDbType.NVarChar, c.Correo, 150); P(cmd, "@EP", SqlDbType.Bit, c.EsPrincipal); P(cmd, "@O", SqlDbType.NVarChar, c.Observacion, 500); await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync(); return Json(new { ok = true, mensaje = "Contacto guardado correctamente." });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudo guardar el contacto: " + ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSede(Sede s)
        {
            if (s.IdUnidad <= 0 || string.IsNullOrWhiteSpace(s.CodigoSede) || string.IsNullOrWhiteSpace(s.NombreSede)) return Json(new { ok = false, mensaje = "Código y nombre de sede son obligatorios." });
            try
            {
                await using var cn = Conexion(); await cn.OpenAsync(); await using var cmd = new SqlCommand(@"IF @I=0 INSERT INTO dbo.Sede(CodigoSede,IdUnidad,NombreSede,IdDepartamento,IdProvincia,IdDistrito,Direccion,Latitud,Longitud,Contacto,Correo,Celular,CentroCostoSede,FechaActivacion,FechaBaja,IdEstado) VALUES(@C,@U,@N,@D,@P,@DI,@A,@LA,@LO,@CT,@CO,@CE,@CC,@FA,@FB,1)
 ELSE UPDATE dbo.Sede SET CodigoSede=@C,NombreSede=@N,IdDepartamento=@D,IdProvincia=@P,IdDistrito=@DI,Direccion=@A,Latitud=@LA,Longitud=@LO,Contacto=@CT,Correo=@CO,Celular=@CE,CentroCostoSede=@CC,FechaActivacion=@FA,FechaBaja=@FB WHERE IdSede=@I AND IdUnidad=@U", cn);
                P(cmd, "@I", SqlDbType.Int, s.IdSede); P(cmd, "@U", SqlDbType.Int, s.IdUnidad); P(cmd, "@C", SqlDbType.VarChar, s.CodigoSede, 20); P(cmd, "@N", SqlDbType.NVarChar, s.NombreSede, 250); P(cmd, "@D", SqlDbType.Int, s.IdDepartamento); P(cmd, "@P", SqlDbType.Int, s.IdProvincia); P(cmd, "@DI", SqlDbType.Int, s.IdDistrito); P(cmd, "@A", SqlDbType.NVarChar, s.Direccion, 300); Dec(cmd, "@LA", s.Latitud); Dec(cmd, "@LO", s.Longitud); P(cmd, "@CT", SqlDbType.NVarChar, s.Contacto, 150); P(cmd, "@CO", SqlDbType.NVarChar, s.Correo, 150); P(cmd, "@CE", SqlDbType.VarChar, s.Celular, 20); P(cmd, "@CC", SqlDbType.NVarChar, s.CentroCostoSede, 100); P(cmd, "@FA", SqlDbType.Date, s.FechaActivacion); P(cmd, "@FB", SqlDbType.Date, s.FechaBaja); await cmd.ExecuteNonQueryAsync(); return Json(new { ok = true, mensaje = "Sede guardada correctamente." });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudo guardar la sede: " + ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> CambiarEstado(int idUnidad, int idEstado) => await Estado("Unidad", "IdUnidad", idUnidad, idEstado);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> CambiarEstadoContacto(int idContactoUnidad, int idEstado) => await Estado("ContactoUnidad", "IdContactoUnidad", idContactoUnidad, idEstado);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> CambiarEstadoSede(int idSede, int idEstado) => await Estado("Sede", "IdSede", idSede, idEstado);
        private async Task<IActionResult> Estado(string t, string pk, int id, int e) { if (id <= 0 || (e != 1 && e != 2)) return Json(new { ok = false, mensaje = "Datos no válidos." }); await using var cn = Conexion(); await cn.OpenAsync(); await using var cmd = new SqlCommand($"UPDATE dbo.{t} SET IdEstado=@E WHERE {pk}=@I", cn); P(cmd, "@E", SqlDbType.Int, e); P(cmd, "@I", SqlDbType.Int, id); var n = await cmd.ExecuteNonQueryAsync(); return Json(new { ok = n > 0, mensaje = n > 0 ? "Estado actualizado correctamente." : "Registro no encontrado." }); }

        private static void P(SqlCommand c, string n, SqlDbType t, object? v, int size = 0) { var p = size > 0 ? c.Parameters.Add(n, t, size) : c.Parameters.Add(n, t); p.Value = v is string s ? (string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim()) : v ?? DBNull.Value; }
        private static void Dec(SqlCommand c, string n, decimal? v) { var p = c.Parameters.Add(n, SqlDbType.Decimal); p.Precision = 10; p.Scale = 7; p.Value = (object?)v ?? DBNull.Value; }
        private static string? T(SqlDataReader r, string c) => r[c] is DBNull ? null : r[c].ToString(); private static int? I(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToInt32(r[c]); private static DateTime? D(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToDateTime(r[c]);
        private static Unidad MapUnidad(SqlDataReader r) => new() { IdUnidad = Convert.ToInt32(r["IdUnidad"]), CodigoUnidad = T(r, "CodigoUnidad") ?? "", IdEmpresa = Convert.ToInt32(r["IdEmpresa"]), EmpresaNombre = T(r, "EmpresaNombre"), RazonSocial = T(r, "RazonSocial") ?? "", RucUnidad = T(r, "RucUnidad"), NombreComercial = T(r, "NombreComercial"), IdSector = I(r, "IdSector"), IdDepartamento = I(r, "IdDepartamento"), IdProvincia = I(r, "IdProvincia"), IdDistrito = I(r, "IdDistrito"), Direccion = T(r, "Direccion"), Telefono = T(r, "Telefono"), Correo = T(r, "Correo"), PaginaWeb = T(r, "PaginaWeb"), CentroCosto = T(r, "CentroCosto"), Observacion = T(r, "Observacion"), IdUsuarioResponsable = I(r, "IdUsuarioResponsable"), FechaActivacion = D(r, "FechaActivacion"), FechaBaja = D(r, "FechaBaja"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), IdEstado = Convert.ToInt32(r["IdEstado"]) };
        private static ContactoUnidad MapContacto(SqlDataReader r) => new() { IdContactoUnidad = Convert.ToInt32(r["IdContactoUnidad"]), IdUnidad = Convert.ToInt32(r["IdUnidad"]), Nombre = T(r, "Nombre") ?? "", Cargo = T(r, "Cargo"), Telefono = T(r, "Telefono"), Correo = T(r, "Correo"), EsPrincipal = Convert.ToBoolean(r["EsPrincipal"]), Observacion = T(r, "Observacion"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), IdEstado = Convert.ToInt32(r["IdEstado"]) };
        private static Sede MapSede(SqlDataReader r) => new() { IdSede = Convert.ToInt32(r["IdSede"]), CodigoSede = T(r, "CodigoSede") ?? "", IdUnidad = Convert.ToInt32(r["IdUnidad"]), NombreSede = T(r, "NombreSede") ?? "", IdDepartamento = I(r, "IdDepartamento"), IdProvincia = I(r, "IdProvincia"), IdDistrito = I(r, "IdDistrito"), Direccion = T(r, "Direccion"), Latitud = r["Latitud"] is DBNull ? null : Convert.ToDecimal(r["Latitud"]), Longitud = r["Longitud"] is DBNull ? null : Convert.ToDecimal(r["Longitud"]), Contacto = T(r, "Contacto"), Correo = T(r, "Correo"), Celular = T(r, "Celular"), CentroCostoSede = T(r, "CentroCostoSede"), FechaActivacion = D(r, "FechaActivacion"), FechaBaja = D(r, "FechaBaja"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), IdEstado = Convert.ToInt32(r["IdEstado"]) };
    }
}
