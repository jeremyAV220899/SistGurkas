namespace SistGurkas.Models.Comercial
{
    public class DocumentoCliente
    {
        public int IdDocumento
        {
            get; set;
        }
        public int IdUnidad
        {
            get; set;
        }
        public int? IdContrato
        {
            get; set;
        }
        public int? IdSede
        {
            get; set;
        }
        public string Nombre { get; set; } = ""; public string Tipo { get; set; } = ""; public string NombreArchivo { get; set; } = ""; public string RutaArchivo { get; set; } = ""; public string? Observacion
        {
            get; set;
        }
        public DateTime FechaRegistro
        {
            get; set;
        }
        public int IdEstado { get; set; } = 1; public string? RelacionadoA
        {
            get; set;
        }
    }
}