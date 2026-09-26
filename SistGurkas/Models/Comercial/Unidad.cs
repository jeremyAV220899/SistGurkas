namespace SistGurkas.Models.Comercial
{
    public class Unidad
    {
        public int IdUnidad { get; set; }
        public string CodigoUnidad { get; set; } = "";
        public int IdEmpresa { get; set; }
        public string? EmpresaNombre { get; set; }
        public string RazonSocial { get; set; } = ""; public string? RucUnidad { get; set; }
        public string? NombreComercial { get; set; }
        public int? IdSector { get; set; }
        public int? IdDepartamento { get; set; }
        public int? IdProvincia { get; set; }
        public int? IdDistrito { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? PaginaWeb { get; set; }
        public string? CentroCosto { get; set; }
        public string? Observacion { get; set; }
        public int? IdUsuarioResponsable { get; set; }
        public DateTime? FechaActivacion { get; set; }
        public DateTime? FechaBaja { get; set; }
        public DateTime FechaRegistro { get; set; }
        public int IdEstado { get; set; }
        public string CodigoCliente => IdUnidad > 0 ? $"CL-{FechaRegistro:yyyy}-{IdUnidad:D3}" : "";
    }
}