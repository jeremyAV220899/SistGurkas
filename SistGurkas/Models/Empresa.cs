namespace SistGurkas.Models
{
    public class Empresa
    {
        public int IdEmpresa
        {
            get; set;
        }
        public string NombreEmpresa { get; set; } = string.Empty;
        public string? Ruc
        {
            get; set;
        }
        public string? Direccion
        {
            get; set;
        }
        public int IdEstado
        {
            get; set;
        }
        public string NombreEstado { get; set; } = string.Empty;
    }
}