using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistGurkas.Models.Comercial;
using System.Data;
using System.Text.Json;
using SistGurkas.Services.OpenAI;

namespace SistGurkas.Controllers.Comercial
{
    public class ClienteController : Controller
    {
        private readonly IConfiguration _configuration; private readonly IWebHostEnvironment _env; private readonly IOpenAiTdrService _openAiTdr;
        public ClienteController(IConfiguration configuration, IWebHostEnvironment env, IOpenAiTdrService openAiTdr)
        {
            _configuration = configuration;
            _env = env;
            _openAiTdr = openAiTdr;
        }
        private SqlConnection Conexion() => new(_configuration.GetConnectionString("SistGurkas") ?? throw new InvalidOperationException("Falta la conexión SistGurkas."));
        private static SqlCommand SP(SqlConnection cn, string nombre)
        {
            var c = new SqlCommand(nombre, cn);
            c.CommandType = CommandType.StoredProcedure;
            return c;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, int? idEmpresa, int? idEstado)
        {
            var lista = new List<Unidad>();
            var empresas = new List<(int Id, string Nombre)>();
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using (var cmd = SP(cn, "dbo.sp_Cliente_Listar"))
            {
                P(cmd, "@Buscar", SqlDbType.NVarChar, buscar, 250);
                P(cmd, "@IdEmpresa", SqlDbType.Int, idEmpresa);
                P(cmd, "@IdEstado", SqlDbType.Int, idEstado);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    lista.Add(MapUnidad(r));
            }
            await using (var cmd = SP(cn, "dbo.sp_Cliente_EmpresasActivas"))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    empresas.Add((Convert.ToInt32(r["IdEmpresa"]), r["NombreEmpresa"]?.ToString() ?? ""));
            }
            ViewBag.Empresas = empresas;
            ViewBag.Buscar = buscar;
            ViewBag.IdEmpresa = idEmpresa;
            ViewBag.IdEstado = idEstado;
            await CargarCatalogosAsync();
            return View("~/Views/Comercial/Cliente/Index.cshtml", lista);
        }

        private async Task CargarCatalogosAsync()
        {
            var sectores = new List<(int Id, string Nombre)>();
            var departamentos = new List<(int Id, string Nombre)>();
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Prospecto_Catalogos");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                sectores.Add((Convert.ToInt32(r["Id"]), r["Nombre"]?.ToString() ?? ""));
            await r.NextResultAsync();
            while (await r.ReadAsync())
            {
            }
            await r.NextResultAsync();
            while (await r.ReadAsync())
                departamentos.Add((Convert.ToInt32(r["Id"]), r["Nombre"]?.ToString() ?? ""));
            ViewBag.Sectores = sectores;
            ViewBag.Departamentos = departamentos;
        }

        [HttpGet]
        public async Task<IActionResult> Provincias(int idDepartamento)
        {
            var lista = new List<object>();
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Ubigeo_Provincias_Listar");
            P(cmd, "@IdDepartamento", SqlDbType.Int, idDepartamento);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new
                {
                    id = Convert.ToInt32(r["IdProvincia"]),
                    nombre = r["Nombre"]?.ToString() ?? ""
                });
            return Json(lista);
        }

        [HttpGet]
        public async Task<IActionResult> Distritos(int idProvincia)
        {
            var lista = new List<object>();
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Ubigeo_Distritos_Listar");
            P(cmd, "@IdProvincia", SqlDbType.Int, idProvincia);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new
                {
                    id = Convert.ToInt32(r["IdDistrito"]),
                    nombre = r["Nombre"]?.ToString() ?? ""
                });
            return Json(lista);
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            if (id <= 0)
                return Json(new
                {
                    ok = false,
                    mensaje = "Cliente no válido."
                });
            var vm = new ClienteDetalleVm();
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Cliente_Detalle");
            P(cmd, "@IdUnidad", SqlDbType.Int, id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return Json(new
                {
                    ok = false,
                    mensaje = "Cliente no encontrado."
                });
            vm.Cliente = MapUnidad(r);
            await r.NextResultAsync();
            while (await r.ReadAsync())
                vm.Contactos.Add(MapContacto(r));
            await r.NextResultAsync();
            while (await r.ReadAsync())
                vm.Sedes.Add(MapSede(r));
            await r.NextResultAsync();
            while (await r.ReadAsync())
                vm.Contratos.Add(MapContrato(r));
            await r.NextResultAsync();
            while (await r.ReadAsync())
                vm.Requerimientos.Add(MapRequerimiento(r));
            await r.NextResultAsync();
            while (await r.ReadAsync())
                vm.Documentos.Add(MapDocumento(r));
            await r.NextResultAsync();
            while (await r.ReadAsync())
                vm.Historial.Add(MapHistorial(r));
            return Json(new
            {
                ok = true,
                data = vm
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(Unidad u)
        {
            if (string.IsNullOrWhiteSpace(u.CodigoUnidad) || u.IdEmpresa <= 0 || string.IsNullOrWhiteSpace(u.RazonSocial))
            {
                TempData["Error"] = "Código, empresa y razón social son obligatorios.";
                return RedirectToAction(nameof(Index));
            }
            if (!string.IsNullOrWhiteSpace(u.RucUnidad) && (u.RucUnidad.Length != 11 || !u.RucUnidad.All(char.IsDigit)))
            {
                TempData["Error"] = "El RUC debe tener 11 dígitos.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                await using var cn = Conexion();
                await cn.OpenAsync();
                await using var cmd = SP(cn, "dbo.sp_Cliente_Guardar");
                P(cmd, "@IdUnidad", SqlDbType.Int, u.IdUnidad);
                P(cmd, "@CodigoUnidad", SqlDbType.VarChar, u.CodigoUnidad, 20);
                P(cmd, "@IdEmpresa", SqlDbType.Int, u.IdEmpresa);
                P(cmd, "@RazonSocial", SqlDbType.NVarChar, u.RazonSocial, 250);
                P(cmd, "@RucUnidad", SqlDbType.VarChar, u.RucUnidad, 11);
                P(cmd, "@NombreComercial", SqlDbType.NVarChar, u.NombreComercial, 150);
                P(cmd, "@IdSector", SqlDbType.Int, u.IdSector);
                P(cmd, "@IdDepartamento", SqlDbType.Int, u.IdDepartamento);
                P(cmd, "@IdProvincia", SqlDbType.Int, u.IdProvincia);
                P(cmd, "@IdDistrito", SqlDbType.Int, u.IdDistrito);
                P(cmd, "@Direccion", SqlDbType.NVarChar, u.Direccion, 300);
                P(cmd, "@Telefono", SqlDbType.VarChar, u.Telefono, 20);
                P(cmd, "@Correo", SqlDbType.NVarChar, u.Correo, 150);
                P(cmd, "@PaginaWeb", SqlDbType.NVarChar, u.PaginaWeb, 250);
                P(cmd, "@CentroCosto", SqlDbType.NVarChar, u.CentroCosto, 100);
                P(cmd, "@Observacion", SqlDbType.NVarChar, u.Observacion, 1000);
                P(cmd, "@FechaActivacion", SqlDbType.Date, u.FechaActivacion);
                P(cmd, "@FechaBaja", SqlDbType.Date, u.FechaBaja);
                await using var r = await cmd.ExecuteReaderAsync();
                await r.ReadAsync();
                TempData[Convert.ToBoolean(r["Ok"]) ? "Success" : "Error"] = r["Mensaje"]?.ToString();
            }
            catch (Exception ex) { TempData["Error"] = "No se pudo guardar: " + ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarContacto(ContactoUnidad c)
        {
            if (c.IdUnidad <= 0 || string.IsNullOrWhiteSpace(c.Nombre))
                return Json(new
                {
                    ok = false,
                    mensaje = "Cliente y nombre son obligatorios."
                });
            try
            {
                await using var cn = Conexion();
                await cn.OpenAsync();
                await using var cmd = SP(cn, "dbo.sp_ContactoUnidad_Guardar");
                P(cmd, "@IdContactoUnidad", SqlDbType.Int, c.IdContactoUnidad);
                P(cmd, "@IdUnidad", SqlDbType.Int, c.IdUnidad);
                P(cmd, "@Nombre", SqlDbType.NVarChar, c.Nombre, 150);
                P(cmd, "@Cargo", SqlDbType.NVarChar, c.Cargo, 100);
                P(cmd, "@Telefono", SqlDbType.VarChar, c.Telefono, 20);
                P(cmd, "@Correo", SqlDbType.NVarChar, c.Correo, 150);
                P(cmd, "@EsPrincipal", SqlDbType.Bit, c.EsPrincipal);
                P(cmd, "@Observacion", SqlDbType.NVarChar, c.Observacion, 500);
                await using var r = await cmd.ExecuteReaderAsync();
                await r.ReadAsync();
                return Json(new
                {
                    ok = Convert.ToBoolean(r["Ok"]),
                    mensaje = r["Mensaje"]?.ToString()
                });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudo guardar el contacto: " + ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSede(Sede s)
        {
            if (s.IdUnidad <= 0 || string.IsNullOrWhiteSpace(s.NombreSede))
                return Json(new
                {
                    ok = false,
                    mensaje = "Cliente y nombre de sede son obligatorios."
                });
            try
            {
                await using var cn = Conexion();
                await cn.OpenAsync();
                await using var cmd = SP(cn, "dbo.sp_Sede_Guardar");
                P(cmd, "@IdSede", SqlDbType.Int, s.IdSede);
                P(cmd, "@IdUnidad", SqlDbType.Int, s.IdUnidad);
                P(cmd, "@CodigoSede", SqlDbType.VarChar, s.CodigoSede, 20);
                P(cmd, "@NombreSede", SqlDbType.NVarChar, s.NombreSede, 250);
                P(cmd, "@IdDepartamento", SqlDbType.Int, s.IdDepartamento);
                P(cmd, "@IdProvincia", SqlDbType.Int, s.IdProvincia);
                P(cmd, "@IdDistrito", SqlDbType.Int, s.IdDistrito);
                P(cmd, "@Direccion", SqlDbType.NVarChar, s.Direccion, 300);
                Dec(cmd, "@Latitud", s.Latitud);
                Dec(cmd, "@Longitud", s.Longitud);
                P(cmd, "@Contacto", SqlDbType.NVarChar, s.Contacto, 150);
                P(cmd, "@Correo", SqlDbType.NVarChar, s.Correo, 150);
                P(cmd, "@Celular", SqlDbType.VarChar, s.Celular, 20);
                P(cmd, "@CentroCostoSede", SqlDbType.NVarChar, s.CentroCostoSede, 100);
                P(cmd, "@FechaActivacion", SqlDbType.Date, s.FechaActivacion);
                P(cmd, "@FechaBaja", SqlDbType.Date, s.FechaBaja);
                await using var r = await cmd.ExecuteReaderAsync();
                await r.ReadAsync();
                return Json(new
                {
                    ok = Convert.ToBoolean(r["Ok"]),
                    mensaje = r["Mensaje"]?.ToString()
                });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudo guardar la sede: " + ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarContrato(ContratoCliente x)
        {
            if (x.IdUnidad <= 0 || string.IsNullOrWhiteSpace(x.NumeroContrato) || string.IsNullOrWhiteSpace(x.Descripcion))
                return Json(new
                {
                    ok = false,
                    mensaje = "Número y descripción son obligatorios."
                });
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_ContratoCliente_Guardar");
            P(cmd, "@IdContrato", SqlDbType.Int, x.IdContrato);
            P(cmd, "@IdUnidad", SqlDbType.Int, x.IdUnidad);
            P(cmd, "@NumeroContrato", SqlDbType.NVarChar, x.NumeroContrato, 50);
            P(cmd, "@Descripcion", SqlDbType.NVarChar, x.Descripcion, 500);
            P(cmd, "@FechaInicio", SqlDbType.Date, x.FechaInicio);
            P(cmd, "@FechaFin", SqlDbType.Date, x.FechaFin);
            Dec18(cmd, "@Monto", x.Monto);
            P(cmd, "@Moneda", SqlDbType.VarChar, x.Moneda, 10);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            return Json(new
            {
                ok = Convert.ToBoolean(r["Ok"]),
                mensaje = T(r, "Mensaje")
            });
        }
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CambiarEstadoContrato(int idContrato, int idEstado) => Estado("dbo.sp_ContratoCliente_CambiarEstado", "@IdContrato", idContrato, idEstado);
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarRequerimiento(RequerimientoCliente x)
        {
            if (x.IdUnidad <= 0 || x.IdContrato <= 0 || x.IdSede <= 0 || string.IsNullOrWhiteSpace(x.PuestoPerfil) || x.Cantidad <= 0)
                return Json(new
                {
                    ok = false,
                    mensaje = "Contrato, sede, puesto y cantidad son obligatorios."
                });
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_RequerimientoCliente_Guardar");
            P(cmd, "@IdRequerimiento", SqlDbType.Int, x.IdRequerimiento);
            P(cmd, "@IdUnidad", SqlDbType.Int, x.IdUnidad);
            P(cmd, "@IdContrato", SqlDbType.Int, x.IdContrato);
            P(cmd, "@IdSede", SqlDbType.Int, x.IdSede);
            P(cmd, "@PuestoPerfil", SqlDbType.NVarChar, x.PuestoPerfil, 200);
            P(cmd, "@Cantidad", SqlDbType.Int, x.Cantidad);
            P(cmd, "@Modalidad", SqlDbType.VarChar, x.Modalidad, 20);
            P(cmd, "@Sexo", SqlDbType.VarChar, x.Sexo, 20);
            P(cmd, "@Horario", SqlDbType.NVarChar, x.Horario, 100);
            P(cmd, "@Dias", SqlDbType.NVarChar, x.Dias, 100);
            P(cmd, "@FechaInicio", SqlDbType.Date, x.FechaInicio);
            P(cmd, "@FechaFin", SqlDbType.Date, x.FechaFin);
            P(cmd, "@RequisitosEspeciales", SqlDbType.NVarChar, x.RequisitosEspeciales, 1000);
            P(cmd, "@RecursosAsociados", SqlDbType.NVarChar, x.RecursosAsociados, 1000);
            P(cmd, "@Observacion", SqlDbType.NVarChar, x.Observacion, 1000);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            return Json(new
            {
                ok = Convert.ToBoolean(r["Ok"]),
                mensaje = T(r, "Mensaje")
            });
        }
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CambiarEstadoRequerimiento(int idRequerimiento, int idEstado) => Estado("dbo.sp_RequerimientoCliente_CambiarEstado", "@IdRequerimiento", idRequerimiento, idEstado);
        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(104857600)]
        public async Task<IActionResult> GuardarDocumento(int idUnidad, int? idContrato, int? idSede, string nombre, string tipo, string? observacion, IFormFile archivo)
        {
            if (idUnidad <= 0 || archivo == null || archivo.Length == 0 || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(tipo))
                return Json(new
                {
                    ok = false,
                    mensaje = "Nombre, tipo y archivo son obligatorios."
                });
            if (archivo.Length > 104857600)
                return Json(new
                {
                    ok = false,
                    mensaje = "El archivo supera 100 MB."
                });
            var ext = Path.GetExtension(archivo.FileName);
            var file = $"{Guid.NewGuid():N}{ext}";
            var dir = Path.Combine(_env.WebRootPath, "uploads", "clientes", idUnidad.ToString());
            Directory.CreateDirectory(dir);
            var full = Path.Combine(dir, file);
            await using (var fs = System.IO.File.Create(full))
                await archivo.CopyToAsync(fs);
            var ruta = $"/uploads/clientes/{idUnidad}/{file}";
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_DocumentoCliente_Registrar");
            P(cmd, "@IdUnidad", SqlDbType.Int, idUnidad);
            P(cmd, "@IdContrato", SqlDbType.Int, idContrato);
            P(cmd, "@IdSede", SqlDbType.Int, idSede);
            P(cmd, "@Nombre", SqlDbType.NVarChar, nombre, 250);
            P(cmd, "@Tipo", SqlDbType.NVarChar, tipo, 80);
            P(cmd, "@NombreArchivo", SqlDbType.NVarChar, Path.GetFileName(archivo.FileName), 260);
            P(cmd, "@RutaArchivo", SqlDbType.NVarChar, ruta, 600);
            P(cmd, "@Observacion", SqlDbType.NVarChar, observacion, 1000);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            return Json(new
            {
                ok = Convert.ToBoolean(r["Ok"]),
                mensaje = T(r, "Mensaje")
            });
        }
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CambiarEstadoDocumento(int idDocumento, int idEstado) => Estado("dbo.sp_DocumentoCliente_CambiarEstado", "@IdDocumento", idDocumento, idEstado);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarTdrIa(int idDocumento, bool forzarReproceso = false, CancellationToken ct = default)
        {
            if (idDocumento <= 0)
                return Json(new
                {
                    ok = false,
                    mensaje = "Documento no válido."
                });
            try
            {
                // Protección de costo: si ya existe un análisis, nunca volvemos a OpenAI salvo reproceso explícito.
                if (!forzarReproceso)
                {
                    await using var cnExist = Conexion();
                    await cnExist.OpenAsync(ct);
                    await using var cmdExist = SP(cnExist, "dbo.sp_AnalisisTdr_Obtener");
                    P(cmdExist, "@IdDocumento", SqlDbType.Int, idDocumento);
                    var existente = await cmdExist.ExecuteScalarAsync(ct);
                    if (existente is not null && existente is not DBNull)
                        return Json(new
                        {
                            ok = true,
                            yaProcesado = true,
                            mensaje = "Este TDR ya fue procesado. Se cargó el análisis guardado sin consumir OpenAI.",
                            data = JsonSerializer.Deserialize<AnalisisTdrIa>(existente.ToString()!)
                        });
                }

                string? ruta = null;
                string? tipo = null;
                int idUnidad = 0;
                await using (var cn = Conexion())
                {
                    await cn.OpenAsync(ct);
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_DocumentoObtener");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    if (!await r.ReadAsync(ct))
                        return Json(new
                        {
                            ok = false,
                            mensaje = "Documento no encontrado."
                        });
                    ruta = T(r, "RutaArchivo");
                    tipo = T(r, "Tipo");
                    idUnidad = Convert.ToInt32(r["IdUnidad"]);
                }
                if (!string.Equals(tipo, "TDR", StringComparison.OrdinalIgnoreCase))
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Solo se pueden analizar documentos de tipo TDR."
                    });
                if (string.IsNullOrWhiteSpace(ruta))
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El documento no tiene archivo asociado."
                    });
                var fisica = Path.Combine(_env.WebRootPath, ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                var ficha = await _openAiTdr.AnalizarPdfAsync(fisica, ct);
                ficha.ComplementoContractualProcesado = true;
                var json = JsonSerializer.Serialize(ficha);
                await using (var cn = Conexion())
                {
                    await cn.OpenAsync(ct);
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_Guardar");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    P(cmd, "@IdUnidad", SqlDbType.Int, idUnidad);
                    P(cmd, "@ResumenEjecutivo", SqlDbType.NVarChar, ficha.ResumenEjecutivo, -1);
                    P(cmd, "@PersonalTotal", SqlDbType.Int, ficha.PersonalTotal);
                    P(cmd, "@Armados", SqlDbType.Int, ficha.Armados);
                    P(cmd, "@Simples", SqlDbType.Int, ficha.Simples);
                    P(cmd, "@SedesDetectadas", SqlDbType.Int, ficha.SedesDetectadas);
                    P(cmd, "@JsonResultado", SqlDbType.NVarChar, json, -1);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                return Json(new
                {
                    ok = true,
                    mensaje = "TDR analizado y guardado correctamente.",
                    data = ficha
                });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudo analizar el TDR: " + ex.Message }); }
        }


        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompletarTdrIa(int idDocumento, CancellationToken ct = default)
        {
            if (idDocumento <= 0)
                return Json(new
                {
                    ok = false,
                    mensaje = "Documento no válido."
                });
            try
            {
                AnalisisTdrIa existente;
                await using (var cn = Conexion())
                {
                    await cn.OpenAsync(ct);
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_Obtener");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    var o = await cmd.ExecuteScalarAsync(ct);
                    if (o is null || o is DBNull)
                        return Json(new
                        {
                            ok = false,
                            mensaje = "Primero debes procesar el TDR base."
                        });
                    existente = JsonSerializer.Deserialize<AnalisisTdrIa>(o.ToString()!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? throw new InvalidOperationException("El análisis almacenado no es válido.");
                }

                // Protección de costo: el complemento solo puede ejecutarse una vez.
                if (existente.ComplementoContractualProcesado)
                    return Json(new
                    {
                        ok = true,
                        yaCompletado = true,
                        mensaje = "El complemento contractual ya fue procesado. No se realizó una nueva llamada a OpenAI.",
                        data = existente
                    });

                string? ruta = null;
                int idUnidad = 0;
                await using (var cn = Conexion())
                {
                    await cn.OpenAsync(ct);
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_DocumentoObtener");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    if (!await r.ReadAsync(ct))
                        return Json(new
                        {
                            ok = false,
                            mensaje = "Documento no encontrado."
                        });
                    ruta = T(r, "RutaArchivo");
                    idUnidad = Convert.ToInt32(r["IdUnidad"]);
                }
                if (string.IsNullOrWhiteSpace(ruta))
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El documento no tiene archivo asociado."
                    });
                var fisica = Path.Combine(_env.WebRootPath, ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                var complemento = await _openAiTdr.CompletarPenalidadesYSedesAsync(fisica, ct);
                existente.Penalidades = complemento.Penalidades ?? [];
                existente.Sedes = complemento.Sedes ?? [];
                if (existente.Sedes.Count > 0 && existente.SedesDetectadas <= 0)
                    existente.SedesDetectadas = existente.Sedes.Count;
                existente.ComplementoContractualProcesado = true;
                var json = JsonSerializer.Serialize(existente);

                await using (var cn = Conexion())
                {
                    await cn.OpenAsync(ct);
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_Guardar");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    P(cmd, "@IdUnidad", SqlDbType.Int, idUnidad);
                    P(cmd, "@ResumenEjecutivo", SqlDbType.NVarChar, existente.ResumenEjecutivo, -1);
                    P(cmd, "@PersonalTotal", SqlDbType.Int, existente.PersonalTotal);
                    P(cmd, "@Armados", SqlDbType.Int, existente.Armados);
                    P(cmd, "@Simples", SqlDbType.Int, existente.Simples);
                    P(cmd, "@SedesDetectadas", SqlDbType.Int, existente.SedesDetectadas);
                    P(cmd, "@JsonResultado", SqlDbType.NVarChar, json, -1);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                return Json(new
                {
                    ok = true,
                    mensaje = "Complemento contractual guardado. El análisis base se conservó intacto.",
                    data = existente
                });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudo completar el análisis: " + ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PreguntarTdrIa(int idDocumento, string pregunta, CancellationToken ct)
        {
            if (idDocumento <= 0 || string.IsNullOrWhiteSpace(pregunta))
                return Json(new
                {
                    ok = false,
                    mensaje = "Escribe una pregunta válida."
                });
            try
            {
                string? jsonAnalisis;
                await using (var cn = Conexion())
                {
                    await cn.OpenAsync(ct);
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_Obtener");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    var o = await cmd.ExecuteScalarAsync(ct);
                    if (o is null || o is DBNull)
                        return Json(new
                        {
                            ok = false,
                            mensaje = "Primero debes procesar este TDR con IA."
                        });
                    jsonAnalisis = o.ToString();
                }
                var respuesta = await _openAiTdr.PreguntarSobreAnalisisAsync(jsonAnalisis!, pregunta.Trim(), ct);
                await using (var cn = Conexion())
                {
                    await cn.OpenAsync(ct);
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_ConsultaRegistrar");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    P(cmd, "@Pregunta", SqlDbType.NVarChar, pregunta, -1);
                    P(cmd, "@Respuesta", SqlDbType.NVarChar, respuesta.Respuesta, -1);
                    P(cmd, "@Fuente", SqlDbType.NVarChar, respuesta.Fuente, 1000);
                    P(cmd, "@TieneEvidencia", SqlDbType.Bit, respuesta.TieneEvidencia);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                return Json(new
                {
                    ok = true,
                    data = respuesta
                });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudo responder: " + ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerAnalisisTdr(int idDocumento)
        {
            if (idDocumento <= 0)
                return Json(new
                {
                    ok = false
                });

            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_Obtener");
            P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);

            var json = await cmd.ExecuteScalarAsync();
            if (json is null || json is DBNull || string.IsNullOrWhiteSpace(json.ToString()))
                return Json(new
                {
                    ok = false
                });

            try
            {
                // El JSON persistido puede tener nombres PascalCase (C#) o camelCase.
                // Lo reconstruimos como modelo y dejamos que ASP.NET lo serialice con
                // la política JSON normal de la aplicación para que JavaScript reciba
                // personalTotal, armados, resumenEjecutivo, etc.
                var ficha = JsonSerializer.Deserialize<AnalisisTdrIa>(
                    json.ToString()!,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (ficha is null)
                    return Json(new
                    {
                        ok = false
                    });
                return Json(ficha);
            }
            catch (JsonException)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "El análisis almacenado no tiene un formato válido."
                });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarSedeTdr(int idUnidad, string nombre, string? direccion, string? contacto, string? celular, string? correo, string? centroCosto, string? fuente)
        {
            if (idUnidad <= 0 || string.IsNullOrWhiteSpace(nombre))
                return Json(new
                {
                    ok = false,
                    mensaje = "La sede detectada no tiene nombre válido."
                });
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Sede_ImportarDesdeTdr");
            P(cmd, "@IdUnidad", SqlDbType.Int, idUnidad);
            P(cmd, "@NombreSede", SqlDbType.NVarChar, nombre.Trim(), 250);
            P(cmd, "@Direccion", SqlDbType.NVarChar, direccion, 300);
            P(cmd, "@Contacto", SqlDbType.NVarChar, contacto, 150);
            P(cmd, "@Celular", SqlDbType.VarChar, celular, 20);
            P(cmd, "@Correo", SqlDbType.NVarChar, correo, 150);
            P(cmd, "@CentroCostoSede", SqlDbType.NVarChar, centroCosto, 100);
            P(cmd, "@FuenteTdr", SqlDbType.NVarChar, fuente, 500);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return Json(new
                {
                    ok = false,
                    mensaje = "No se recibió respuesta al importar la sede."
                });
            return Json(new
            {
                ok = Convert.ToBoolean(r["Ok"]),
                mensaje = T(r, "Mensaje"),
                idSede = I(r, "IdSede")
            });
        }



        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PublicarDatosContractualesTdr(int idDocumento, string tipo)
        {
            if (idDocumento <= 0)
                return Json(new
                {
                    ok = false,
                    mensaje = "Documento no válido."
                });
            tipo = (tipo ?? "").Trim().ToUpperInvariant();
            if (tipo != "REQUERIMIENTOS" && tipo != "PENALIDAD")
                return Json(new
                {
                    ok = false,
                    mensaje = "Tipo de publicación no válido."
                });
            try
            {
                string? json;
                await using (var cn = Conexion())
                {
                    await cn.OpenAsync();
                    await using var cmd = SP(cn, "dbo.sp_AnalisisTdr_Obtener");
                    P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                    var o = await cmd.ExecuteScalarAsync();
                    json = o is null || o is DBNull ? null : o.ToString();
                }
                if (string.IsNullOrWhiteSpace(json))
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El TDR no tiene análisis almacenado."
                    });
                var ficha = JsonSerializer.Deserialize<AnalisisTdrIa>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (ficha is null)
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se pudo leer el análisis almacenado."
                    });
                int publicados = 0, omitidos = 0;
                await using var cn2 = Conexion();
                await cn2.OpenAsync();
                if (tipo == "REQUERIMIENTOS")
                {
                    if ((ficha.Sedes?.Count ?? 0) > 0)
                        foreach (var s in ficha.Sedes!)
                        {
                            if (s.Armados > 0)
                            {
                                await using var cmd = SP(cn2, "dbo.sp_TdrRequerimiento_Publicar");
                                PubReq(cmd, idDocumento, "PERSONAL", "Agente de seguridad - Armado", s.Armados, "Persona", "Armado", s.Horario, null, "RRHH|OPERACIONES", s.Fuente);
                                cmd.Parameters["@NombreSede"].Value = s.Nombre;
                                await using var r = await cmd.ExecuteReaderAsync();
                                if (await r.ReadAsync() && Convert.ToBoolean(r["Ok"]))
                                    publicados++;
                                else
                                    omitidos++;
                            }
                            if (s.Simples > 0)
                            {
                                await using var cmd = SP(cn2, "dbo.sp_TdrRequerimiento_Publicar");
                                PubReq(cmd, idDocumento, "PERSONAL", "Agente de seguridad - Simple", s.Simples, "Persona", "Simple", s.Horario, null, "RRHH|OPERACIONES", s.Fuente);
                                cmd.Parameters["@NombreSede"].Value = s.Nombre;
                                await using var r = await cmd.ExecuteReaderAsync();
                                if (await r.ReadAsync() && Convert.ToBoolean(r["Ok"]))
                                    publicados++;
                                else
                                    omitidos++;
                            }
                        }
                    else
                        foreach (var x in ficha.Personal ?? [])
                        {
                            await using var cmd = SP(cn2, "dbo.sp_TdrRequerimiento_Publicar");
                            PubReq(cmd, idDocumento, "PERSONAL", x.PuestoPerfil, x.Cantidad, "Persona", x.Modalidad, x.Horario, null, "RRHH|OPERACIONES", x.Fuente);
                            await using var r = await cmd.ExecuteReaderAsync();
                            if (await r.ReadAsync() && Convert.ToBoolean(r["Ok"]))
                                publicados++;
                            else
                                omitidos++;
                        }
                    foreach (var x in ficha.Recursos ?? [])
                    {
                        var area = AreaRecurso(x.Descripcion);
                        await using var cmd = SP(cn2, "dbo.sp_TdrRequerimiento_Publicar");
                        PubReq(cmd, idDocumento, "EQUIPO", x.Descripcion, ExtraerCantidadInicial(x.Descripcion) ?? 0, "Unidad", null, null, x.Descripcion, area, x.Fuente);
                        await using var r = await cmd.ExecuteReaderAsync();
                        if (await r.ReadAsync() && Convert.ToBoolean(r["Ok"]))
                            publicados++;
                        else
                            omitidos++;
                    }
                    foreach (var x in ficha.Requisitos ?? [])
                    {
                        var area = AreaRequisito(x.Descripcion);
                        await using var cmd = SP(cn2, "dbo.sp_TdrRequerimiento_Publicar");
                        PubReq(cmd, idDocumento, "DOCUMENTACION", x.Descripcion, 0, "", null, null, x.Descripcion, area, x.Fuente);
                        await using var r = await cmd.ExecuteReaderAsync();
                        if (await r.ReadAsync() && Convert.ToBoolean(r["Ok"]))
                            publicados++;
                        else
                            omitidos++;
                    }
                }
                else
                    foreach (var x in ficha.Penalidades ?? [])
                    {
                        var (cat, area) = ClasificarPenalidad(x.Incumplimiento);
                        await using var cmd = SP(cn2, "dbo.sp_PenalidadContractual_Publicar");
                        P(cmd, "@IdDocumento", SqlDbType.Int, idDocumento);
                        P(cmd, "@Descripcion", SqlDbType.NVarChar, x.Incumplimiento, 1000);
                        P(cmd, "@Penalidad", SqlDbType.NVarChar, x.Penalidad, 500);
                        P(cmd, "@Calculo", SqlDbType.NVarChar, x.Calculo, 500);
                        P(cmd, "@Frecuencia", SqlDbType.NVarChar, x.Frecuencia, 500);
                        P(cmd, "@Fuente", SqlDbType.NVarChar, x.Fuente, 1000);
                        P(cmd, "@Categoria", SqlDbType.NVarChar, cat, 80);
                        P(cmd, "@AreaResponsable", SqlDbType.NVarChar, area, 150);
                        await using var r = await cmd.ExecuteReaderAsync();
                        if (await r.ReadAsync() && Convert.ToBoolean(r["Ok"]))
                            publicados++;
                        else
                            omitidos++;
                    }
                return Json(new
                {
                    ok = true,
                    mensaje = $"{publicados} registro(s) publicados. {omitidos} duplicado(s) omitidos. No se consumieron tokens de IA."
                });
            }
            catch (Exception ex) { return Json(new { ok = false, mensaje = "No se pudieron publicar los datos contractuales: " + ex.Message }); }
        }
        private static void PubReq(SqlCommand cmd, int doc, string cat, string desc, int cant, string unidad, string? modalidad, string? horario, string? esp, string area, string fuente)
        {
            P(cmd, "@IdDocumento", SqlDbType.Int, doc);
            P(cmd, "@Categoria", SqlDbType.VarChar, cat, 30);
            P(cmd, "@Subcategoria", SqlDbType.NVarChar, null, 80);
            P(cmd, "@Descripcion", SqlDbType.NVarChar, desc, 1500);
            P(cmd, "@Cantidad", SqlDbType.Int, cant);
            P(cmd, "@UnidadMedida", SqlDbType.NVarChar, unidad, 50);
            P(cmd, "@Modalidad", SqlDbType.NVarChar, modalidad, 50);
            P(cmd, "@Horario", SqlDbType.NVarChar, horario, 150);
            P(cmd, "@Especificacion", SqlDbType.NVarChar, esp, 2000);
            P(cmd, "@Frecuencia", SqlDbType.NVarChar, null, 300);
            P(cmd, "@AreaResponsable", SqlDbType.NVarChar, area, 100);
            P(cmd, "@Fuente", SqlDbType.NVarChar, fuente, 1000);
            P(cmd, "@NombreSede", SqlDbType.NVarChar, null, 250);
        }
        private static string AreaRecurso(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            if (s.Contains("arma") || s.Contains("revólver") || s.Contains("munici"))
                return "ARMAMENTO|OPERACIONES";
            if (s.Contains("epp") || s.Contains("casco") || s.Contains("bota") || s.Contains("chaleco"))
                return "LOGISTICA|SST";
            return "LOGISTICA|OPERACIONES";
        }
        private static string AreaRequisito(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            if (s.Contains("sucamec") || s.Contains("carné") || s.Contains("experiencia") || s.Contains("personal"))
                return "RRHH|OPERACIONES";
            if (s.Contains("sst") || s.Contains("seguridad y salud"))
                return "SST|OPERACIONES";
            if (s.Contains("póliza") || s.Contains("seguro"))
                return "ADMINISTRACION|OPERACIONES";
            return "COMERCIAL|OPERACIONES";
        }
        private static (string, string) ClasificarPenalidad(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            if (s.Contains("uniform") || s.Contains("equipo") || s.Contains("implement"))
                return ("Logística / Equipamiento", "LOGISTICA|OPERACIONES|FINANZAS");
            if (s.Contains("dorm") || s.Contains("celular") || s.Contains("conduct"))
                return ("Conducta / Seguridad", "OPERACIONES|RRHH|FINANZAS");
            if (s.Contains("informe") || s.Contains("document") || s.Contains("sucamec"))
                return ("Documentaria", "OPERACIONES|ADMINISTRACION|FINANZAS");
            if (s.Contains("horario") || s.Contains("relevo") || s.Contains("puesto") || s.Contains("ronda"))
                return ("Operativa", "OPERACIONES|FINANZAS");
            return ("Contractual", "COMERCIAL|OPERACIONES|FINANZAS");
        }

        private static int? ExtraerCantidadInicial(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;
            var m = System.Text.RegularExpressions.Regex.Match(texto.Trim(), @"^(\d+)\b");
            return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
        }

        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CambiarEstado(int idUnidad, int idEstado) => Estado("dbo.sp_Cliente_CambiarEstado", "@IdUnidad", idUnidad, idEstado);
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CambiarEstadoContacto(int idContactoUnidad, int idEstado) => Estado("dbo.sp_ContactoUnidad_CambiarEstado", "@IdContactoUnidad", idContactoUnidad, idEstado);
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CambiarEstadoSede(int idSede, int idEstado) => Estado("dbo.sp_Sede_CambiarEstado", "@IdSede", idSede, idEstado);

        private async Task<IActionResult> Estado(string sp, string pId, int id, int e)
        {
            if (id <= 0 || (e != 1 && e != 2))
                return Json(new
                {
                    ok = false,
                    mensaje = "Datos no válidos."
                });
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, sp);
            P(cmd, pId, SqlDbType.Int, id);
            P(cmd, "@IdEstado", SqlDbType.Int, e);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return Json(new
                {
                    ok = false,
                    mensaje = "No se obtuvo respuesta."
                });
            return Json(new
            {
                ok = Convert.ToBoolean(r["Ok"]),
                mensaje = r["Mensaje"]?.ToString()
            });
        }

        private static void P(SqlCommand c, string n, SqlDbType t, object? v, int size = 0)
        {
            var p = size > 0 ? c.Parameters.Add(n, t, size) : c.Parameters.Add(n, t);
            p.Value = v is string s ? (string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim()) : v ?? DBNull.Value;
        }
        private static void Dec18(SqlCommand c, string n, decimal? v)
        {
            var p = c.Parameters.Add(n, SqlDbType.Decimal);
            p.Precision = 18;
            p.Scale = 2;
            p.Value = (object?)v ?? DBNull.Value;
        }
        private static void Dec(SqlCommand c, string n, decimal? v)
        {
            var p = c.Parameters.Add(n, SqlDbType.Decimal);
            p.Precision = 10;
            p.Scale = 7;
            p.Value = (object?)v ?? DBNull.Value;
        }
        private static string? T(SqlDataReader r, string c) => r[c] is DBNull ? null : r[c].ToString();
        private static int? I(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToInt32(r[c]);
        private static DateTime? D(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToDateTime(r[c]);
        private static Unidad MapUnidad(SqlDataReader r) => new() { IdUnidad = Convert.ToInt32(r["IdUnidad"]), CodigoUnidad = T(r, "CodigoUnidad") ?? "", IdEmpresa = Convert.ToInt32(r["IdEmpresa"]), EmpresaNombre = T(r, "EmpresaNombre"), RazonSocial = T(r, "RazonSocial") ?? "", RucUnidad = T(r, "RucUnidad"), NombreComercial = T(r, "NombreComercial"), IdSector = I(r, "IdSector"), IdDepartamento = I(r, "IdDepartamento"), IdProvincia = I(r, "IdProvincia"), IdDistrito = I(r, "IdDistrito"), Direccion = T(r, "Direccion"), Telefono = T(r, "Telefono"), Correo = T(r, "Correo"), PaginaWeb = T(r, "PaginaWeb"), CentroCosto = T(r, "CentroCosto"), Observacion = T(r, "Observacion"), IdUsuarioResponsable = I(r, "IdUsuarioResponsable"), FechaActivacion = D(r, "FechaActivacion"), FechaBaja = D(r, "FechaBaja"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), IdEstado = Convert.ToInt32(r["IdEstado"]) };
        private static ContactoUnidad MapContacto(SqlDataReader r) => new() { IdContactoUnidad = Convert.ToInt32(r["IdContactoUnidad"]), IdUnidad = Convert.ToInt32(r["IdUnidad"]), Nombre = T(r, "Nombre") ?? "", Cargo = T(r, "Cargo"), Telefono = T(r, "Telefono"), Correo = T(r, "Correo"), EsPrincipal = Convert.ToBoolean(r["EsPrincipal"]), Observacion = T(r, "Observacion"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), IdEstado = Convert.ToInt32(r["IdEstado"]) };

        private static ContratoCliente MapContrato(SqlDataReader r) => new() { IdContrato = Convert.ToInt32(r["IdContrato"]), IdUnidad = Convert.ToInt32(r["IdUnidad"]), NumeroContrato = T(r, "NumeroContrato") ?? "", Descripcion = T(r, "Descripcion") ?? "", FechaInicio = D(r, "FechaInicio"), FechaFin = D(r, "FechaFin"), Monto = r["Monto"] is DBNull ? null : Convert.ToDecimal(r["Monto"]), Moneda = T(r, "Moneda") ?? "PEN", IdEstado = Convert.ToInt32(r["IdEstado"]), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]) };
        private static RequerimientoCliente MapRequerimiento(SqlDataReader r) => new() { IdRequerimiento = Convert.ToInt32(r["IdRequerimiento"]), IdUnidad = Convert.ToInt32(r["IdUnidad"]), IdContrato = Convert.ToInt32(r["IdContrato"]), IdSede = Convert.ToInt32(r["IdSede"]), PuestoPerfil = T(r, "PuestoPerfil") ?? "", Cantidad = Convert.ToInt32(r["Cantidad"]), Modalidad = T(r, "Modalidad") ?? "", Sexo = T(r, "Sexo") ?? "", Horario = T(r, "Horario"), Dias = T(r, "Dias"), FechaInicio = D(r, "FechaInicio"), FechaFin = D(r, "FechaFin"), RequisitosEspeciales = T(r, "RequisitosEspeciales"), RecursosAsociados = T(r, "RecursosAsociados"), Observacion = T(r, "Observacion"), IdEstado = Convert.ToInt32(r["IdEstado"]), NumeroContrato = T(r, "NumeroContrato"), NombreSede = T(r, "NombreSede") };
        private static DocumentoCliente MapDocumento(SqlDataReader r) => new() { IdDocumento = Convert.ToInt32(r["IdDocumento"]), IdUnidad = Convert.ToInt32(r["IdUnidad"]), IdContrato = I(r, "IdContrato"), IdSede = I(r, "IdSede"), Nombre = T(r, "Nombre") ?? "", Tipo = T(r, "Tipo") ?? "", NombreArchivo = T(r, "NombreArchivo") ?? "", RutaArchivo = T(r, "RutaArchivo") ?? "", Observacion = T(r, "Observacion"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), IdEstado = Convert.ToInt32(r["IdEstado"]), RelacionadoA = T(r, "RelacionadoA") };
        private static HistorialCliente MapHistorial(SqlDataReader r) => new() { IdHistorial = Convert.ToInt32(r["IdHistorial"]), IdUnidad = Convert.ToInt32(r["IdUnidad"]), FechaHora = Convert.ToDateTime(r["FechaHora"]), Usuario = T(r, "Usuario") ?? "Sistema", Evento = T(r, "Evento") ?? "", Detalle = T(r, "Detalle") };
        private static Sede MapSede(SqlDataReader r) => new() { IdSede = Convert.ToInt32(r["IdSede"]), CodigoSede = T(r, "CodigoSede") ?? "", IdUnidad = Convert.ToInt32(r["IdUnidad"]), NombreSede = T(r, "NombreSede") ?? "", IdDepartamento = I(r, "IdDepartamento"), IdProvincia = I(r, "IdProvincia"), IdDistrito = I(r, "IdDistrito"), Direccion = T(r, "Direccion"), Latitud = r["Latitud"] is DBNull ? null : Convert.ToDecimal(r["Latitud"]), Longitud = r["Longitud"] is DBNull ? null : Convert.ToDecimal(r["Longitud"]), Contacto = T(r, "Contacto"), Correo = T(r, "Correo"), Celular = T(r, "Celular"), CentroCostoSede = T(r, "CentroCostoSede"), FechaActivacion = D(r, "FechaActivacion"), FechaBaja = D(r, "FechaBaja"), FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]), IdEstado = Convert.ToInt32(r["IdEstado"]) };
    }
}
