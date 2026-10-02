namespace SistGurkas.Models.Comercial
{
    public class Sede
    {
        public int IdSede
        {
            get; set;
        }
        public string CodigoSede { get; set; } = ""; public int IdUnidad
        {
            get; set;
        }
        public string NombreSede { get; set; } = "";
        public int? IdDepartamento
        {
            get; set;
        }
        public int? IdProvincia
        {
            get; set;
        }
        public int? IdDistrito
        {
            get; set;
        }
        public string? Direccion
        {
            get; set;
        }
        public decimal? Latitud
        {
            get; set;
        }
        public decimal? Longitud
        {
            get; set;
        }
        public string? Contacto
        {
            get; set;
        }
        public string? Correo
        {
            get; set;
        }
        public string? Celular
        {
            get; set;
        }
        public string? CentroCostoSede
        {
            get; set;
        }
        public DateTime? FechaActivacion
        {
            get; set;
        }
        public DateTime? FechaBaja
        {
            get; set;
        }
        public DateTime FechaRegistro
        {
            get; set;
        }
        public int IdEstado
        {
            get; set;
        }
    }
}