using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistGurkas.Models;
using System.Data;

namespace SistGurkas.Controllers
{
    public class EmpresaController : Controller
    {
        private readonly IConfiguration _configuration;

        public EmpresaController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection CrearConexion()
        {
            var cadena = _configuration.GetConnectionString("SistGurkas")
                ?? throw new InvalidOperationException(
                    "No se encontró la conexión SistGurkas.");

            return new SqlConnection(cadena);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var empresas = new List<Empresa>();

            const string sql = """
                SELECT e.IdEmpresa, e.NombreEmpresa, e.Ruc,
                       e.Direccion, e.IdEstado, s.NombreEstado
                FROM dbo.Empresa e
                INNER JOIN dbo.Estado s ON s.IdEstado = e.IdEstado
                ORDER BY e.IdEmpresa;
                """;

            await using var conexion = CrearConexion();
            await conexion.OpenAsync();

            await using var comando = new SqlCommand(sql, conexion);
            await using var lector = await comando.ExecuteReaderAsync();

            while (await lector.ReadAsync())
            {
                empresas.Add(new Empresa
                {
                    IdEmpresa = lector.GetInt32(
                        lector.GetOrdinal("IdEmpresa")),
                    NombreEmpresa = lector.GetString(
                        lector.GetOrdinal("NombreEmpresa")),
                    Ruc = lector.IsDBNull(lector.GetOrdinal("Ruc"))
                        ? null
                        : lector.GetString(lector.GetOrdinal("Ruc")),
                    Direccion = lector.IsDBNull(
                        lector.GetOrdinal("Direccion"))
                        ? null
                        : lector.GetString(
                            lector.GetOrdinal("Direccion")),
                    IdEstado = lector.GetInt32(
                        lector.GetOrdinal("IdEstado")),
                    NombreEstado = lector.GetString(
                        lector.GetOrdinal("NombreEstado"))
                });
            }

            return View(empresas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(
    string nombreEmpresa,
    string? ruc,
    string? direccion,
    int idEstado)
        {
            nombreEmpresa = nombreEmpresa?.Trim() ?? "";
            ruc = string.IsNullOrWhiteSpace(ruc) ? null : ruc.Trim();
            direccion = string.IsNullOrWhiteSpace(direccion)
                ? null
                : direccion.Trim();

            if (nombreEmpresa.Length == 0 ||
                nombreEmpresa.Length > 200)
            {
                TempData["Error"] =
                    "La razón social es obligatoria y debe tener máximo 200 caracteres.";

                return RedirectToAction(nameof(Index));
            }

            if (ruc != null &&
                (ruc.Length != 11 || !ruc.All(char.IsDigit)))
            {
                TempData["Error"] = "El RUC debe tener exactamente 11 dígitos.";
                return RedirectToAction(nameof(Index));
            }

            if (direccion?.Length > 500)
            {
                TempData["Error"] =
                    "La dirección no debe superar 500 caracteres.";

                return RedirectToAction(nameof(Index));
            }

            if (idEstado != 1 && idEstado != 2)
            {
                TempData["Error"] = "Selecciona un estado válido.";
                return RedirectToAction(nameof(Index));
            }

            const string sql = """
        INSERT INTO dbo.Empresa
            (NombreEmpresa, Ruc, Direccion, IdEstado)
        VALUES
            (@NombreEmpresa, @Ruc, @Direccion, @IdEstado);
        """;

            try
            {
                await using var conexion = CrearConexion();
                await conexion.OpenAsync();

                await using var comando = new SqlCommand(sql, conexion);

                comando.Parameters.Add("@NombreEmpresa",
                    SqlDbType.NVarChar, 200).Value = nombreEmpresa;

                comando.Parameters.Add("@Ruc",
                    SqlDbType.VarChar, 11).Value =
                    (object?)ruc ?? DBNull.Value;

                comando.Parameters.Add("@Direccion",
                    SqlDbType.NVarChar, 500).Value =
                    (object?)direccion ?? DBNull.Value;

                comando.Parameters.Add("@IdEstado",
                    SqlDbType.Int).Value = idEstado;

                await comando.ExecuteNonQueryAsync();

                TempData["Exito"] = "Empresa registrada correctamente.";
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                TempData["Error"] =
                    "El RUC ingresado ya está registrado en otra empresa.";
            }
            catch (SqlException)
            {
                TempData["Error"] =
                    "No se pudo registrar la empresa. Inténtalo nuevamente.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> VerificarRuc(string ruc)
        {
            ruc = ruc?.Trim() ?? "";

            if (ruc.Length != 11 || !ruc.All(char.IsDigit))
            {
                return Json(new
                {
                    valido = false,
                    existe = false
                });
            }

            const string sql = """
        SELECT COUNT(1)
        FROM dbo.Empresa
        WHERE Ruc = @Ruc;
        """;

            await using var conexion = CrearConexion();
            await conexion.OpenAsync();

            await using var comando = new SqlCommand(sql, conexion);
            comando.Parameters.Add("@Ruc", SqlDbType.VarChar, 11).Value = ruc;

            var cantidad = Convert.ToInt32(
                await comando.ExecuteScalarAsync());

            return Json(new
            {
                valido = true,
                existe = cantidad > 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(
            int idEmpresa,
            int idEstado)
        {
            if (idEstado != 1 && idEstado != 2)
            {
                return BadRequest(new
                {
                    ok = false,
                    mensaje = "Estado no válido."
                });
            }

            const string sql = """
        UPDATE dbo.Empresa
        SET IdEstado = @IdEstado
        WHERE IdEmpresa = @IdEmpresa;
        """;

            await using var conexion = CrearConexion();
            await conexion.OpenAsync();

            await using var comando = new SqlCommand(sql, conexion);

            comando.Parameters.Add("@IdEmpresa", SqlDbType.Int)
                .Value = idEmpresa;

            comando.Parameters.Add("@IdEstado", SqlDbType.Int)
                .Value = idEstado;

            var filas = await comando.ExecuteNonQueryAsync();

            if (filas == 0)
            {
                return NotFound(new
                {
                    ok = false,
                    mensaje = "La empresa no existe."
                });
            }

            return Json(new
            {
                ok = true,
                mensaje = "Estado actualizado correctamente."
            });
        }
    }
}