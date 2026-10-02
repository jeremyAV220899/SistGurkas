using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistGurkas.Models.Comercial;
using System.Data;

namespace SistGurkas.Controllers.Comercial
{
    public class OportunidadController : Controller
    {
        private readonly IConfiguration _configuration;

        public OportunidadController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection Conexion()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("SistGurkas")
                ?? throw new InvalidOperationException("Falta la conexión SistGurkas.")
            );
        }

        private static SqlCommand SP(SqlConnection cn, string nombre)
        {
            var cmd = new SqlCommand(nombre, cn);
            cmd.CommandType = CommandType.StoredProcedure;
            return cmd;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, int? idEstadoOportunidad)
        {
            var lista = new List<Oportunidad>();
            var prospectos = new List<(int Id, string Nombre)>();

            await using var cn = Conexion();
            await cn.OpenAsync();

            await using (var cmd = SP(cn, "dbo.sp_Oportunidad_Listar"))
            {
                cmd.Parameters.Add("@Buscar", SqlDbType.NVarChar, 200).Value =
                    string.IsNullOrWhiteSpace(buscar) ? DBNull.Value : buscar.Trim();

                cmd.Parameters.Add("@IdEstadoOportunidad", SqlDbType.Int).Value =
                    (object?)idEstadoOportunidad ?? DBNull.Value;

                await using var r = await cmd.ExecuteReaderAsync();

                while (await r.ReadAsync())
                    lista.Add(MapearOportunidad(r));
            }

            await using (var cmd = SP(cn, "dbo.sp_Oportunidad_ProspectosActivos"))
            {
                await using var r = await cmd.ExecuteReaderAsync();

                while (await r.ReadAsync())
                {
                    prospectos.Add((
                        Convert.ToInt32(r["IdProspecto"]),
                        r["RazonSocial"]?.ToString() ?? ""
                    ));
                }
            }

            ViewBag.Prospectos = prospectos;
            ViewBag.Buscar = buscar;
            ViewBag.Estado = idEstadoOportunidad;

            return View("~/Views/Comercial/Oportunidad/Index.cshtml", lista);
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            if (id <= 0)
                return Json(new
                {
                    ok = false,
                    mensaje = "Oportunidad no válida."
                });

            Oportunidad oportunidad;
            object prospecto;
            int? idUnidad;
            string? codigoUnidad;
            var contactos = new List<object>();
            var servicios = new List<object>();
            var seguimientos = new List<object>();
            var documentos = new List<object>();

            await using (var cn = Conexion())
            {
                await cn.OpenAsync();
                await using var cmd = SP(cn, "dbo.sp_Oportunidad_DetalleCompleto");
                cmd.Parameters.Add("@IdOportunidad", SqlDbType.Int).Value = id;
                await using var r = await cmd.ExecuteReaderAsync();

                if (!await r.ReadAsync())
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Oportunidad no encontrada."
                    });

                oportunidad = MapearOportunidad(r);
                prospecto = new
                {
                    idProspecto = Convert.ToInt32(r["IdProspecto"]),
                    razonSocial = Texto(r, "ProspectoNombre"),
                    ruc = Texto(r, "Ruc"),
                    nombreComercial = Texto(r, "NombreComercial"),
                    direccion = Texto(r, "Direccion"),
                    telefono = Texto(r, "Telefono"),
                    correo = Texto(r, "Correo"),
                    paginaWeb = Texto(r, "PaginaWeb"),
                    idSector = NullableInt(r, "IdSector"),
                    idDepartamento = NullableInt(r, "IdDepartamento"),
                    idProvincia = NullableInt(r, "IdProvincia"),
                    idDistrito = NullableInt(r, "IdDistrito"),
                    tipoCliente = Texto(r, "TipoCliente"),
                    competenciaActual = Texto(r, "CompetenciaActual")
                };
                idUnidad = NullableInt(r, "IdUnidad");
                codigoUnidad = Texto(r, "CodigoUnidad");

                await r.NextResultAsync();
                while (await r.ReadAsync())
                    contactos.Add(new
                    {
                        id = Convert.ToInt32(r["IdContactoProspecto"]),
                        nombre = Texto(r, "Nombre"),
                        cargo = Texto(r, "Cargo"),
                        telefono = Texto(r, "Telefono"),
                        correo = Texto(r, "Correo"),
                        esPrincipal = Convert.ToBoolean(r["EsPrincipal"])
                    });

                await r.NextResultAsync();
                while (await r.ReadAsync())
                    servicios.Add(new
                    {
                        id = Convert.ToInt32(r["IdServicioOportunidad"]),
                        servicio = Texto(r, "Servicio"),
                        cantidad = Convert.ToInt32(r["Cantidad"]),
                        tipo = Texto(r, "Tipo"),
                        jornada = Texto(r, "Jornada"),
                        observaciones = Texto(r, "Observaciones")
                    });

                await r.NextResultAsync();
                while (await r.ReadAsync())
                    seguimientos.Add(new
                    {
                        id = Convert.ToInt32(r["IdSeguimientoOportunidad"]),
                        fechaGestion = Convert.ToDateTime(r["FechaGestion"]),
                        tipo = Texto(r, "Tipo"),
                        comentario = Texto(r, "Comentario"),
                        proximaAccion = Texto(r, "ProximaAccion"),
                        fechaProximaAccion = NullableFecha(r, "FechaProximaAccion"),
                        idUsuarioResponsable = NullableInt(r, "IdUsuarioResponsable")
                    });

                await r.NextResultAsync();
                while (await r.ReadAsync())
                    documentos.Add(new
                    {
                        id = Convert.ToInt32(r["IdDocumentoOportunidad"]),
                        nombre = Texto(r, "Nombre"),
                        tipo = Texto(r, "Tipo"),
                        nombreArchivo = Texto(r, "NombreArchivo"),
                        rutaArchivo = Texto(r, "RutaArchivo"),
                        observacion = Texto(r, "Observacion"),
                        fechaRegistro = Convert.ToDateTime(r["FechaRegistro"])
                    });
            }

            object? propuesta = null;
            var competencias = new List<object>();
            var historial = new List<object>();

            await using (var cn = Conexion())
            {
                await cn.OpenAsync();
                await using var cmd = SP(cn, "dbo.sp_Oportunidad_Complementos");
                P(cmd, "@IdOportunidad", SqlDbType.Int, id);
                await using var r = await cmd.ExecuteReaderAsync();

                if (await r.ReadAsync())
                    propuesta = new
                    {
                        id = Convert.ToInt32(r["IdPropuestaOportunidad"]),
                        version = Texto(r, "Version"),
                        fechaEnvio = NullableFecha(r, "FechaEnvio"),
                        monto = r["Monto"] is DBNull ? (decimal?)null : Convert.ToDecimal(r["Monto"]),
                        moneda = Texto(r, "Moneda"),
                        vigenciaDias = NullableInt(r, "VigenciaDias"),
                        estado = Texto(r, "Estado"),
                        observacion = Texto(r, "Observacion"),
                        idDocumentoOportunidad = NullableInt(r, "IdDocumentoOportunidad")
                    };

                await r.NextResultAsync();
                while (await r.ReadAsync())
                    competencias.Add(new
                    {
                        id = Convert.ToInt32(r["IdCompetenciaOportunidad"]),
                        competidor = Texto(r, "Competidor"),
                        montoConocido = r["MontoConocido"] is DBNull ? (decimal?)null : Convert.ToDecimal(r["MontoConocido"]),
                        moneda = Texto(r, "Moneda"),
                        fortalezas = Texto(r, "Fortalezas"),
                        debilidades = Texto(r, "Debilidades"),
                        observacion = Texto(r, "Observacion"),
                        esPrincipal = Convert.ToBoolean(r["EsPrincipal"])
                    });

                await r.NextResultAsync();
                while (await r.ReadAsync())
                    historial.Add(new
                    {
                        id = Convert.ToInt32(r["IdHistorialOportunidad"]),
                        fecha = Convert.ToDateTime(r["FechaRegistro"]),
                        evento = Texto(r, "Evento"),
                        detalle = Texto(r, "Detalle"),
                        usuario = Texto(r, "Usuario")
                    });
            }

            return Json(new
            {
                ok = true,
                oportunidad,
                prospecto,
                contactos,
                servicios,
                seguimientos,
                documentos,
                propuesta,
                competencias,
                historial,
                conversion = new
                {
                    convertido = idUnidad.HasValue,
                    idUnidad,
                    codigoCliente = codigoUnidad
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(Oportunidad o)
        {
            if (o.IdProspecto <= 0 || string.IsNullOrWhiteSpace(o.Nombre))
            {
                TempData["Error"] =
                    "Selecciona un prospecto e ingresa el nombre de la oportunidad.";
                return RedirectToAction(nameof(Index));
            }

            if (o.ProbabilidadCierre is < 0 or > 100)
            {
                TempData["Error"] = "La probabilidad debe estar entre 0 y 100.";
                return RedirectToAction(nameof(Index));
            }

            if (o.MontoEstimado < 0)
            {
                TempData["Error"] = "El monto estimado no puede ser negativo.";
                return RedirectToAction(nameof(Index));
            }

            if (o.IdEstadoOportunidad < 1 || o.IdEstadoOportunidad > 5)
            {
                TempData["Error"] = "La etapa comercial no es válida.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await using var cn = Conexion();
                await cn.OpenAsync();

                await using var cmd = SP(cn, "dbo.sp_Oportunidad_Guardar");

                cmd.Parameters.Add("@IdOportunidad", SqlDbType.Int).Value = o.IdOportunidad;
                cmd.Parameters.Add("@IdProspecto", SqlDbType.Int).Value = o.IdProspecto;
                cmd.Parameters.Add("@Nombre", SqlDbType.NVarChar, 200).Value = o.Nombre.Trim();
                cmd.Parameters.Add("@DescripcionNecesidad", SqlDbType.NVarChar, 2000).Value = Db(o.DescripcionNecesidad);
                cmd.Parameters.Add("@ObjetivosAlcance", SqlDbType.NVarChar, 2000).Value = Db(o.ObjetivosAlcance);
                cmd.Parameters.Add("@Observacion", SqlDbType.NVarChar, 2000).Value = Db(o.Observacion);
                cmd.Parameters.Add("@IdEstadoOportunidad", SqlDbType.Int).Value = o.IdEstadoOportunidad;
                cmd.Parameters.Add("@ProbabilidadCierre", SqlDbType.Int).Value =
                    (object?)o.ProbabilidadCierre ?? DBNull.Value;
                cmd.Parameters.Add("@FechaEstimadaCierre", SqlDbType.Date).Value =
                    (object?)o.FechaEstimadaCierre ?? DBNull.Value;

                var monto = cmd.Parameters.Add("@MontoEstimado", SqlDbType.Decimal);
                monto.Precision = 18;
                monto.Scale = 2;
                monto.Value = (object?)o.MontoEstimado ?? DBNull.Value;

                cmd.Parameters.Add("@Moneda", SqlDbType.VarChar, 3).Value =
                    o.Moneda == "USD" ? "USD" : "PEN";
                cmd.Parameters.Add("@TipoContratacion", SqlDbType.NVarChar, 100).Value = Db(o.TipoContratacion);
                cmd.Parameters.Add("@Prioridad", SqlDbType.VarChar, 20).Value = Db(o.Prioridad);
                cmd.Parameters.Add("@FechaAceptacion", SqlDbType.Date).Value =
                    (object?)o.FechaAceptacion ?? DBNull.Value;

                await using var r = await cmd.ExecuteReaderAsync();

                if (!await r.ReadAsync())
                {
                    TempData["Error"] = "No se obtuvo respuesta al guardar la oportunidad.";
                    return RedirectToAction(nameof(Index));
                }

                var ok = Convert.ToBoolean(r["Ok"]);
                var mensaje = r["Mensaje"]?.ToString() ?? "Operación finalizada.";

                TempData[ok ? "Success" : "Error"] = mensaje;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR OPORTUNIDAD: " + ex);
                TempData["Error"] =
                    "No se pudo guardar la oportunidad: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int idOportunidad, int idEstado)
        {
            if (idOportunidad <= 0 || (idEstado != 1 && idEstado != 2))
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "Datos no válidos."
                });
            }

            try
            {
                await using var cn = Conexion();
                await cn.OpenAsync();

                await using var cmd = SP(cn, "dbo.sp_Oportunidad_CambiarEstado");
                cmd.Parameters.Add("@IdOportunidad", SqlDbType.Int).Value = idOportunidad;
                cmd.Parameters.Add("@IdEstado", SqlDbType.Int).Value = idEstado;

                await using var r = await cmd.ExecuteReaderAsync();

                if (!await r.ReadAsync())
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se obtuvo respuesta del procedimiento."
                    });

                return Json(new
                {
                    ok = Convert.ToBoolean(r["Ok"]),
                    mensaje = r["Mensaje"]?.ToString() ?? "Operación finalizada."
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo actualizar el estado."
                });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarContactoProspecto(int idContactoProspecto, int idOportunidad, string nombre, string? cargo, string? telefono, string? correo, bool esPrincipal)
        {
            if (idOportunidad <= 0 || string.IsNullOrWhiteSpace(nombre))
                return Json(new
                {
                    ok = false,
                    mensaje = "El nombre del contacto es obligatorio."
                });

            return await EjecutarRespuesta("dbo.sp_ContactoProspecto_GuardarDesdeOportunidad", cmd =>
            {
                P(cmd, "@IdContactoProspecto", SqlDbType.Int, idContactoProspecto);
                P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad);
                P(cmd, "@Nombre", SqlDbType.NVarChar, nombre, 150);
                P(cmd, "@Cargo", SqlDbType.NVarChar, cargo, 100);
                P(cmd, "@Telefono", SqlDbType.VarChar, telefono, 20);
                P(cmd, "@Correo", SqlDbType.NVarChar, correo, 150);
                P(cmd, "@EsPrincipal", SqlDbType.Bit, esPrincipal);
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> DarBajaContactoProspecto(int idContactoProspecto, int idOportunidad) =>
            EjecutarRespuesta("dbo.sp_ContactoProspecto_DarBajaDesdeOportunidad", cmd =>
            {
                P(cmd, "@IdContactoProspecto", SqlDbType.Int, idContactoProspecto);
                P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad);
            });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarServicio(int idServicioOportunidad, int idOportunidad, string servicio, int cantidad, string? tipo, string? jornada, string? observaciones)
        {
            if (idOportunidad <= 0 || string.IsNullOrWhiteSpace(servicio) || cantidad <= 0)
                return Json(new
                {
                    ok = false,
                    mensaje = "Completa servicio y cantidad."
                });
            return await EjecutarRespuesta("dbo.sp_ServicioOportunidad_Guardar", cmd => { P(cmd, "@IdServicioOportunidad", SqlDbType.Int, idServicioOportunidad); P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad); P(cmd, "@Servicio", SqlDbType.NVarChar, servicio, 200); P(cmd, "@Cantidad", SqlDbType.Int, cantidad); P(cmd, "@Tipo", SqlDbType.VarChar, tipo, 30); P(cmd, "@Jornada", SqlDbType.NVarChar, jornada, 100); P(cmd, "@Observaciones", SqlDbType.NVarChar, observaciones, 1000); });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> EliminarServicio(int idServicioOportunidad, int idOportunidad) => EjecutarRespuesta("dbo.sp_ServicioOportunidad_Eliminar", cmd => { P(cmd, "@IdServicioOportunidad", SqlDbType.Int, idServicioOportunidad); P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad); });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarSeguimiento(int idOportunidad, DateTime fechaGestion, string tipo, string? comentario, string? proximaAccion, DateTime? fechaProximaAccion)
        {
            if (idOportunidad <= 0 || string.IsNullOrWhiteSpace(tipo))
                return Json(new
                {
                    ok = false,
                    mensaje = "Completa fecha y tipo de gestión."
                });
            return await EjecutarRespuesta("dbo.sp_SeguimientoOportunidad_Registrar", cmd => { P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad); P(cmd, "@FechaGestion", SqlDbType.DateTime2, fechaGestion); P(cmd, "@Tipo", SqlDbType.NVarChar, tipo, 60); P(cmd, "@Comentario", SqlDbType.NVarChar, comentario, 1500); P(cmd, "@ProximaAccion", SqlDbType.NVarChar, proximaAccion, 500); P(cmd, "@FechaProximaAccion", SqlDbType.DateTime2, fechaProximaAccion); P(cmd, "@IdUsuarioResponsable", SqlDbType.Int, null); });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(100L * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 100L * 1024 * 1024)]
        public async Task<IActionResult> RegistrarDocumento(int idOportunidad, string nombre, string tipo, string? observacion, IFormFile? archivo)
        {
            if (idOportunidad <= 0 || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(tipo))
                return Json(new
                {
                    ok = false,
                    mensaje = "Nombre y tipo de documento son obligatorios."
                });

            string? rutaFisica = null;
            try
            {
                string? nombreArchivo = null, ruta = null;
                if (archivo is { Length: > 0 })
                {
                    var permitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg" };
                    var ext = Path.GetExtension(archivo.FileName);
                    if (!permitidas.Contains(ext))
                        return Json(new
                        {
                            ok = false,
                            mensaje = "Tipo de archivo no permitido."
                        });
                    if (archivo.Length > 100L * 1024 * 1024)
                        return Json(new
                        {
                            ok = false,
                            mensaje = "El archivo supera el límite máximo permitido de 100 MB."
                        });

                    nombreArchivo = Path.GetFileName(archivo.FileName);
                    var seguro = $"{Guid.NewGuid():N}{ext}";
                    var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "oportunidades", idOportunidad.ToString());
                    Directory.CreateDirectory(carpeta);
                    rutaFisica = Path.Combine(carpeta, seguro);
                    await using (var fs = System.IO.File.Create(rutaFisica))
                        await archivo.CopyToAsync(fs);
                    ruta = $"/uploads/oportunidades/{idOportunidad}/{seguro}";
                }

                await using var cn = Conexion();
                await cn.OpenAsync();
                await using var cmd = SP(cn, "dbo.sp_DocumentoOportunidad_Registrar");
                P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad);
                P(cmd, "@Nombre", SqlDbType.NVarChar, nombre, 250);
                P(cmd, "@Tipo", SqlDbType.NVarChar, tipo, 80);
                P(cmd, "@NombreArchivo", SqlDbType.NVarChar, nombreArchivo, 260);
                P(cmd, "@RutaArchivo", SqlDbType.NVarChar, ruta, 600);
                P(cmd, "@Observacion", SqlDbType.NVarChar, observacion, 500);
                await using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                    throw new InvalidOperationException("El procedimiento no devolvió respuesta.");
                var ok = Convert.ToBoolean(r["Ok"]);
                var mensaje = Texto(r, "Mensaje") ?? "Operación finalizada.";
                if (!ok && rutaFisica != null && System.IO.File.Exists(rutaFisica))
                    System.IO.File.Delete(rutaFisica);
                return Json(new
                {
                    ok,
                    mensaje
                });
            }
            catch (Exception ex)
            {
                if (rutaFisica != null && System.IO.File.Exists(rutaFisica))
                    System.IO.File.Delete(rutaFisica);
                System.Diagnostics.Debug.WriteLine(ex);
                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo registrar el documento: " + ex.Message
                });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> EliminarDocumento(int idDocumentoOportunidad, int idOportunidad) => EjecutarRespuesta("dbo.sp_DocumentoOportunidad_Eliminar", cmd => { P(cmd, "@IdDocumentoOportunidad", SqlDbType.Int, idDocumentoOportunidad); P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad); });

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> GuardarPropuesta(int idOportunidad, string version, DateTime? fechaEnvio, decimal? monto, string? moneda, int? vigenciaDias, string estado, string? observacion, int? idDocumentoOportunidad)
        {
            if (idOportunidad <= 0 || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(estado))
                return Task.FromResult<IActionResult>(Json(new
                {
                    ok = false,
                    mensaje = "Versión y estado son obligatorios."
                }));

            return EjecutarRespuesta("dbo.sp_PropuestaOportunidad_Guardar", cmd =>
            {
                P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad);
                P(cmd, "@Version", SqlDbType.NVarChar, version, 30);
                P(cmd, "@FechaEnvio", SqlDbType.Date, fechaEnvio);
                var pm = cmd.Parameters.Add("@Monto", SqlDbType.Decimal);
                pm.Precision = 18;
                pm.Scale = 2;
                pm.Value = (object?)monto ?? DBNull.Value;
                P(cmd, "@Moneda", SqlDbType.VarChar, string.IsNullOrWhiteSpace(moneda) ? "PEN" : moneda, 3);
                P(cmd, "@VigenciaDias", SqlDbType.Int, vigenciaDias);
                P(cmd, "@Estado", SqlDbType.NVarChar, estado, 50);
                P(cmd, "@Observacion", SqlDbType.NVarChar, observacion, 1000);
                P(cmd, "@IdDocumentoOportunidad", SqlDbType.Int, idDocumentoOportunidad);
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> GuardarCompetencia(int idCompetenciaOportunidad, int idOportunidad, string competidor, decimal? montoConocido, string? moneda, string? fortalezas, string? debilidades, string? observacion, bool esPrincipal)
        {
            if (idOportunidad <= 0 || string.IsNullOrWhiteSpace(competidor))
                return Task.FromResult<IActionResult>(Json(new
                {
                    ok = false,
                    mensaje = "El nombre del competidor es obligatorio."
                }));

            return EjecutarRespuesta("dbo.sp_CompetenciaOportunidad_Guardar", cmd =>
            {
                P(cmd, "@IdCompetenciaOportunidad", SqlDbType.Int, idCompetenciaOportunidad);
                P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad);
                P(cmd, "@Competidor", SqlDbType.NVarChar, competidor, 200);
                var pm = cmd.Parameters.Add("@MontoConocido", SqlDbType.Decimal);
                pm.Precision = 18;
                pm.Scale = 2;
                pm.Value = (object?)montoConocido ?? DBNull.Value;
                P(cmd, "@Moneda", SqlDbType.VarChar, string.IsNullOrWhiteSpace(moneda) ? "PEN" : moneda, 3);
                P(cmd, "@Fortalezas", SqlDbType.NVarChar, fortalezas, 1000);
                P(cmd, "@Debilidades", SqlDbType.NVarChar, debilidades, 1000);
                P(cmd, "@Observacion", SqlDbType.NVarChar, observacion, 1000);
                P(cmd, "@EsPrincipal", SqlDbType.Bit, esPrincipal);
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> EliminarCompetencia(int idCompetenciaOportunidad, int idOportunidad) =>
            EjecutarRespuesta("dbo.sp_CompetenciaOportunidad_Eliminar", cmd =>
            {
                P(cmd, "@IdCompetenciaOportunidad", SqlDbType.Int, idCompetenciaOportunidad);
                P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad);
            });

        [HttpGet]
        public async Task<IActionResult> DatosConversion(int id)
        {
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Oportunidad_DatosConversion");
            P(cmd, "@IdOportunidad", SqlDbType.Int, id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return Json(new
                {
                    ok = false,
                    mensaje = "Oportunidad no encontrada."
                });
            var datos = new
            {
                idOportunidad = Convert.ToInt32(r["IdOportunidad"]),
                etapa = Convert.ToInt32(r["IdEstadoOportunidad"]),
                idProspecto = Convert.ToInt32(r["IdProspecto"]),
                razonSocial = Texto(r, "RazonSocial"),
                ruc = Texto(r, "Ruc"),
                nombreComercial = Texto(r, "NombreComercial"),
                idSector = NullableInt(r, "IdSector"),
                idDepartamento = NullableInt(r, "IdDepartamento"),
                idProvincia = NullableInt(r, "IdProvincia"),
                idDistrito = NullableInt(r, "IdDistrito"),
                direccion = Texto(r, "Direccion"),
                telefono = Texto(r, "Telefono"),
                correo = Texto(r, "Correo"),
                paginaWeb = Texto(r, "PaginaWeb"),
                tipoCliente = Texto(r, "TipoCliente"),
                idUnidad = NullableInt(r, "IdUnidad")
            };
            var contactos = new List<object>();
            await r.NextResultAsync();
            while (await r.ReadAsync())
                contactos.Add(new
                {
                    id = Convert.ToInt32(r["IdContactoProspecto"]),
                    nombre = Texto(r, "Nombre"),
                    cargo = Texto(r, "Cargo"),
                    telefono = Texto(r, "Telefono"),
                    correo = Texto(r, "Correo"),
                    esPrincipal = Convert.ToBoolean(r["EsPrincipal"])
                });
            var empresas = new List<object>();
            await r.NextResultAsync();
            while (await r.ReadAsync())
                empresas.Add(new
                {
                    id = Convert.ToInt32(r["IdEmpresa"]),
                    nombre = Texto(r, "NombreEmpresa")
                });
            var sectores = new List<object>();
            await r.NextResultAsync();
            while (await r.ReadAsync())
                sectores.Add(new
                {
                    id = Convert.ToInt32(r["IdSector"]),
                    nombre = Texto(r, "Nombre")
                });
            var departamentos = new List<object>();
            await r.NextResultAsync();
            while (await r.ReadAsync())
                departamentos.Add(new
                {
                    id = Convert.ToInt32(r["IdDepartamento"]),
                    nombre = Texto(r, "Nombre")
                });
            return Json(new
            {
                ok = true,
                datos,
                contactos,
                empresas,
                sectores,
                departamentos
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertirCliente(int idOportunidad, int idEmpresa, string razonSocial, string? rucUnidad, string? nombreComercial, int? idSector, int? idDepartamento, int? idProvincia, int? idDistrito, string? direccion, string? telefono, string? correo, string? paginaWeb, string? tipoCliente, bool copiarContactos = true)
        {
            if (idOportunidad <= 0 || idEmpresa <= 0 || string.IsNullOrWhiteSpace(razonSocial))
                return Json(new
                {
                    ok = false,
                    mensaje = "Empresa interna y razón social son obligatorias."
                });
            if (!string.IsNullOrWhiteSpace(rucUnidad) && (rucUnidad.Length != 11 || !rucUnidad.All(char.IsDigit)))
                return Json(new
                {
                    ok = false,
                    mensaje = "El RUC debe tener 11 dígitos."
                });
            await using var cn = Conexion();
            await cn.OpenAsync();
            await using var cmd = SP(cn, "dbo.sp_Oportunidad_ConvertirCliente");
            P(cmd, "@IdOportunidad", SqlDbType.Int, idOportunidad);
            P(cmd, "@IdEmpresa", SqlDbType.Int, idEmpresa);
            P(cmd, "@RazonSocial", SqlDbType.NVarChar, razonSocial, 250);
            P(cmd, "@RucUnidad", SqlDbType.VarChar, rucUnidad, 11);
            P(cmd, "@NombreComercial", SqlDbType.NVarChar, nombreComercial, 150);
            P(cmd, "@IdSector", SqlDbType.Int, idSector);
            P(cmd, "@IdDepartamento", SqlDbType.Int, idDepartamento);
            P(cmd, "@IdProvincia", SqlDbType.Int, idProvincia);
            P(cmd, "@IdDistrito", SqlDbType.Int, idDistrito);
            P(cmd, "@Direccion", SqlDbType.NVarChar, direccion, 300);
            P(cmd, "@Telefono", SqlDbType.VarChar, telefono, 20);
            P(cmd, "@Correo", SqlDbType.NVarChar, correo, 150);
            P(cmd, "@PaginaWeb", SqlDbType.NVarChar, paginaWeb, 250);
            P(cmd, "@TipoCliente", SqlDbType.VarChar, tipoCliente, 20);
            P(cmd, "@CopiarContactos", SqlDbType.Bit, copiarContactos);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            return Json(new
            {
                ok = Convert.ToBoolean(r["Ok"]),
                mensaje = Texto(r, "Mensaje"),
                idUnidad = NullableInt(r, "IdUnidad"),
                codigoCliente = Texto(r, "CodigoCliente")
            });
        }

        private async Task<IActionResult> EjecutarRespuesta(string sp, Action<SqlCommand> parametros)
        {
            try
            {
                await using var cn = Conexion();
                await cn.OpenAsync();
                await using var cmd = SP(cn, sp);
                parametros(cmd);
                await using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Sin respuesta del procedimiento."
                    });
                return Json(new
                {
                    ok = Convert.ToBoolean(r["Ok"]),
                    mensaje = Texto(r, "Mensaje")
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); return Json(new { ok = false, mensaje = "No se pudo completar la operación." }); }
        }
        private static void P(SqlCommand c, string n, SqlDbType t, object? v, int size = 0)
        {
            var p = size > 0 ? c.Parameters.Add(n, t, size) : c.Parameters.Add(n, t);
            p.Value = v is string s ? (string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim()) : v ?? DBNull.Value;
        }

        private static Oportunidad MapearOportunidad(SqlDataReader r)
        {
            return new Oportunidad
            {
                IdOportunidad = Convert.ToInt32(r["IdOportunidad"]),
                IdProspecto = Convert.ToInt32(r["IdProspecto"]),
                ProspectoNombre = Texto(r, "ProspectoNombre"),
                Nombre = Texto(r, "Nombre") ?? "",
                DescripcionNecesidad = Texto(r, "DescripcionNecesidad"),
                ObjetivosAlcance = Texto(r, "ObjetivosAlcance"),
                Observacion = Texto(r, "Observacion"),
                IdUsuarioResponsable = NullableInt(r, "IdUsuarioResponsable"),
                IdEstadoOportunidad = Convert.ToInt32(r["IdEstadoOportunidad"]),
                ProbabilidadCierre = NullableInt(r, "ProbabilidadCierre"),
                FechaEstimadaCierre = NullableFecha(r, "FechaEstimadaCierre"),
                MontoEstimado = r["MontoEstimado"] is DBNull
                    ? null : Convert.ToDecimal(r["MontoEstimado"]),
                Moneda = Texto(r, "Moneda") ?? "PEN",
                TipoContratacion = Texto(r, "TipoContratacion"),
                Prioridad = Texto(r, "Prioridad"),
                FechaAceptacion = NullableFecha(r, "FechaAceptacion"),
                FechaRegistro = Convert.ToDateTime(r["FechaRegistro"]),
                IdEstado = Convert.ToInt32(r["IdEstado"])
            };
        }

        private static object Db(string? texto)
        {
            return string.IsNullOrWhiteSpace(texto)
                ? DBNull.Value
                : texto.Trim();
        }

        private static string? Texto(SqlDataReader r, string columna)
        {
            return r[columna] is DBNull ? null : r[columna].ToString();
        }

        private static int? NullableInt(SqlDataReader r, string columna)
        {
            return r[columna] is DBNull ? null : Convert.ToInt32(r[columna]);
        }

        private static DateTime? NullableFecha(SqlDataReader r, string columna)
        {
            return r[columna] is DBNull ? null : Convert.ToDateTime(r[columna]);
        }
    }
}
