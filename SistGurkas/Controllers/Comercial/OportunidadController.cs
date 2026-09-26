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
                ?? throw new InvalidOperationException(
                    "Falta la conexión SistGurkas.")
            );
        }

        // ============================================================
        // LISTADO
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? buscar,
            int? idEstadoOportunidad)
        {
            var lista = new List<Oportunidad>();

            await using var cn = Conexion();
            await cn.OpenAsync();

            const string sql = @"
SELECT
    o.*,
    p.RazonSocial AS ProspectoNombre
FROM dbo.Oportunidad o
INNER JOIN dbo.Prospecto p
    ON p.IdProspecto = o.IdProspecto
WHERE
(
    @Buscar IS NULL
    OR o.Nombre LIKE '%' + @Buscar + '%'
    OR p.RazonSocial LIKE '%' + @Buscar + '%'
)
AND
(
    @Estado IS NULL
    OR o.IdEstadoOportunidad = @Estado
)
ORDER BY o.IdOportunidad DESC;";

            await using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add(
                    "@Buscar",
                    SqlDbType.NVarChar,
                    200
                ).Value =
                    string.IsNullOrWhiteSpace(buscar)
                    ? DBNull.Value
                    : buscar.Trim();

                cmd.Parameters.Add(
                    "@Estado",
                    SqlDbType.Int
                ).Value =
                    (object?)idEstadoOportunidad
                    ?? DBNull.Value;

                await using var r =
                    await cmd.ExecuteReaderAsync();

                while (await r.ReadAsync())
                {
                    lista.Add(MapearOportunidad(r));
                }
            }

            // Prospectos para el modal
            var prospectos =
                new List<(int Id, string Nombre)>();

            const string sqlProspectos = @"
SELECT
    IdProspecto,
    RazonSocial
FROM dbo.Prospecto
WHERE IdEstado = 1
ORDER BY RazonSocial;";

            await using (var cmd =
                new SqlCommand(sqlProspectos, cn))
            {
                await using var r =
                    await cmd.ExecuteReaderAsync();

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

            return View(
                "~/Views/Comercial/Oportunidad/Index.cshtml",
                lista
            );
        }

        // ============================================================
        // DETALLE
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            if (id <= 0)
                return NotFound();

            await using var cn = Conexion();
            await cn.OpenAsync();

            const string sql = @"
SELECT
    o.*,
    p.RazonSocial AS ProspectoNombre
FROM dbo.Oportunidad o
INNER JOIN dbo.Prospecto p
    ON p.IdProspecto = o.IdProspecto
WHERE o.IdOportunidad = @Id;";

            await using var cmd =
                new SqlCommand(sql, cn);

            cmd.Parameters.Add(
                "@Id",
                SqlDbType.Int
            ).Value = id;

            await using var r =
                await cmd.ExecuteReaderAsync();

            if (!await r.ReadAsync())
                return NotFound();

            var oportunidad =
                MapearOportunidad(r);

            return View(
                "~/Views/Comercial/Oportunidad/Detalle.cshtml",
                oportunidad
            );
        }

        // ============================================================
        // CREAR / EDITAR
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(
            Oportunidad o)
        {
            if (o.IdProspecto <= 0 ||
                string.IsNullOrWhiteSpace(o.Nombre))
            {
                TempData["Error"] =
                    "Selecciona un prospecto e ingresa el nombre de la oportunidad.";

                return RedirectToAction(nameof(Index));
            }

            if (o.ProbabilidadCierre is < 0 or > 100)
            {
                TempData["Error"] =
                    "La probabilidad debe estar entre 0 y 100.";

                return RedirectToAction(nameof(Index));
            }

            if (o.MontoEstimado < 0)
            {
                TempData["Error"] =
                    "El monto estimado no puede ser negativo.";

                return RedirectToAction(nameof(Index));
            }

            if (o.IdEstadoOportunidad < 1 ||
                o.IdEstadoOportunidad > 5)
            {
                TempData["Error"] =
                    "La etapa comercial no es válida.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                await using var cn = Conexion();
                await cn.OpenAsync();

                const string sql = @"
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.Prospecto
    WHERE IdProspecto = @IdProspecto
      AND IdEstado = 1
)
BEGIN
    THROW 50001,
    'El prospecto no existe o está inactivo.',
    1;
END;

IF @IdOportunidad = 0
BEGIN

    INSERT INTO dbo.Oportunidad
    (
        IdProspecto,
        Nombre,
        DescripcionNecesidad,
        ObjetivosAlcance,
        Observacion,
        IdUsuarioResponsable,
        IdEstadoOportunidad,
        ProbabilidadCierre,
        FechaEstimadaCierre,
        MontoEstimado,
        Moneda,
        TipoContratacion,
        Prioridad,
        FechaAceptacion,
        IdEstado
    )
    VALUES
    (
        @IdProspecto,
        @Nombre,
        @DescripcionNecesidad,
        @ObjetivosAlcance,
        @Observacion,
        NULL,
        @IdEstadoOportunidad,
        @ProbabilidadCierre,
        @FechaEstimadaCierre,
        @MontoEstimado,
        @Moneda,
        @TipoContratacion,
        @Prioridad,
        @FechaAceptacion,
        1
    );

END
ELSE
BEGIN

    UPDATE dbo.Oportunidad
    SET
        IdProspecto = @IdProspecto,
        Nombre = @Nombre,
        DescripcionNecesidad =
            @DescripcionNecesidad,
        ObjetivosAlcance =
            @ObjetivosAlcance,
        Observacion =
            @Observacion,
        IdEstadoOportunidad =
            @IdEstadoOportunidad,
        ProbabilidadCierre =
            @ProbabilidadCierre,
        FechaEstimadaCierre =
            @FechaEstimadaCierre,
        MontoEstimado =
            @MontoEstimado,
        Moneda =
            @Moneda,
        TipoContratacion =
            @TipoContratacion,
        Prioridad =
            @Prioridad,
        FechaAceptacion =
            @FechaAceptacion
    WHERE IdOportunidad =
        @IdOportunidad
      AND IdEstado = 1;

    IF @@ROWCOUNT = 0
    BEGIN
        THROW 50002,
        'No se encontró una oportunidad activa para editar.',
        1;
    END;

END;";

                await using var cmd =
                    new SqlCommand(sql, cn);

                cmd.Parameters.Add(
                    "@IdOportunidad",
                    SqlDbType.Int
                ).Value = o.IdOportunidad;

                cmd.Parameters.Add(
                    "@IdProspecto",
                    SqlDbType.Int
                ).Value = o.IdProspecto;

                cmd.Parameters.Add(
                    "@Nombre",
                    SqlDbType.NVarChar,
                    200
                ).Value = o.Nombre.Trim();

                cmd.Parameters.Add(
                    "@DescripcionNecesidad",
                    SqlDbType.NVarChar,
                    2000
                ).Value = Db(o.DescripcionNecesidad);

                cmd.Parameters.Add(
                    "@ObjetivosAlcance",
                    SqlDbType.NVarChar,
                    2000
                ).Value = Db(o.ObjetivosAlcance);

                cmd.Parameters.Add(
                    "@Observacion",
                    SqlDbType.NVarChar,
                    2000
                ).Value = Db(o.Observacion);

                cmd.Parameters.Add(
                    "@IdEstadoOportunidad",
                    SqlDbType.Int
                ).Value = o.IdEstadoOportunidad;

                cmd.Parameters.Add(
                    "@ProbabilidadCierre",
                    SqlDbType.Int
                ).Value =
                    (object?)o.ProbabilidadCierre
                    ?? DBNull.Value;

                cmd.Parameters.Add(
                    "@FechaEstimadaCierre",
                    SqlDbType.Date
                ).Value =
                    (object?)o.FechaEstimadaCierre
                    ?? DBNull.Value;

                var monto =
                    cmd.Parameters.Add(
                        "@MontoEstimado",
                        SqlDbType.Decimal
                    );

                monto.Precision = 18;
                monto.Scale = 2;
                monto.Value =
                    (object?)o.MontoEstimado
                    ?? DBNull.Value;

                cmd.Parameters.Add(
                    "@Moneda",
                    SqlDbType.VarChar,
                    3
                ).Value =
                    o.Moneda == "USD"
                    ? "USD"
                    : "PEN";

                cmd.Parameters.Add(
                    "@TipoContratacion",
                    SqlDbType.NVarChar,
                    100
                ).Value =
                    Db(o.TipoContratacion);

                cmd.Parameters.Add(
                    "@Prioridad",
                    SqlDbType.VarChar,
                    20
                ).Value =
                    Db(o.Prioridad);

                cmd.Parameters.Add(
                    "@FechaAceptacion",
                    SqlDbType.Date
                ).Value =
                    (object?)o.FechaAceptacion
                    ?? DBNull.Value;

                await cmd.ExecuteNonQueryAsync();

                TempData["Success"] =
                    o.IdOportunidad == 0
                    ? "Oportunidad registrada correctamente."
                    : "Oportunidad actualizada correctamente.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "ERROR OPORTUNIDAD: " + ex
                );

                TempData["Error"] =
                    "No se pudo guardar la oportunidad: "
                    + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ACTIVAR / DESACTIVAR
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(
            int idOportunidad,
            int idEstado)
        {
            if (idOportunidad <= 0 ||
                (idEstado != 1 && idEstado != 2))
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

                const string sql = @"
UPDATE dbo.Oportunidad
SET IdEstado = @Estado
WHERE IdOportunidad = @Id;";

                await using var cmd =
                    new SqlCommand(sql, cn);

                cmd.Parameters.Add(
                    "@Estado",
                    SqlDbType.Int
                ).Value = idEstado;

                cmd.Parameters.Add(
                    "@Id",
                    SqlDbType.Int
                ).Value = idOportunidad;

                var filas =
                    await cmd.ExecuteNonQueryAsync();

                return Json(new
                {
                    ok = filas > 0,
                    mensaje = filas > 0
                        ? "Estado actualizado correctamente."
                        : "No se encontró la oportunidad."
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo actualizar el estado."
                });
            }
        }

        // ============================================================
        // MAPEO
        // ============================================================
        private static Oportunidad MapearOportunidad(
            SqlDataReader r)
        {
            return new Oportunidad
            {
                IdOportunidad =
                    Convert.ToInt32(
                        r["IdOportunidad"]),

                IdProspecto =
                    Convert.ToInt32(
                        r["IdProspecto"]),

                ProspectoNombre =
                    Texto(
                        r,
                        "ProspectoNombre"),

                Nombre =
                    Texto(
                        r,
                        "Nombre") ?? "",

                DescripcionNecesidad =
                    Texto(
                        r,
                        "DescripcionNecesidad"),

                ObjetivosAlcance =
                    Texto(
                        r,
                        "ObjetivosAlcance"),

                Observacion =
                    Texto(
                        r,
                        "Observacion"),

                IdUsuarioResponsable =
                    NullableInt(
                        r,
                        "IdUsuarioResponsable"),

                IdEstadoOportunidad =
                    Convert.ToInt32(
                        r["IdEstadoOportunidad"]),

                ProbabilidadCierre =
                    NullableInt(
                        r,
                        "ProbabilidadCierre"),

                FechaEstimadaCierre =
                    NullableFecha(
                        r,
                        "FechaEstimadaCierre"),

                MontoEstimado =
                    r["MontoEstimado"] is DBNull
                    ? null
                    : Convert.ToDecimal(
                        r["MontoEstimado"]),

                Moneda =
                    Texto(
                        r,
                        "Moneda") ?? "PEN",

                TipoContratacion =
                    Texto(
                        r,
                        "TipoContratacion"),

                Prioridad =
                    Texto(
                        r,
                        "Prioridad"),

                FechaAceptacion =
                    NullableFecha(
                        r,
                        "FechaAceptacion"),

                FechaRegistro =
                    Convert.ToDateTime(
                        r["FechaRegistro"]),

                IdEstado =
                    Convert.ToInt32(
                        r["IdEstado"])
            };
        }

        // ============================================================
        // HELPERS
        // ============================================================
        private static object Db(string? texto)
        {
            return string.IsNullOrWhiteSpace(texto)
                ? DBNull.Value
                : texto.Trim();
        }

        private static string? Texto(
            SqlDataReader r,
            string columna)
        {
            return r[columna] is DBNull
                ? null
                : r[columna].ToString();
        }

        private static int? NullableInt(
            SqlDataReader r,
            string columna)
        {
            return r[columna] is DBNull
                ? null
                : Convert.ToInt32(
                    r[columna]);
        }

        private static DateTime? NullableFecha(
            SqlDataReader r,
            string columna)
        {
            return r[columna] is DBNull
                ? null
                : Convert.ToDateTime(
                    r[columna]);
        }
    }
}