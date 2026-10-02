namespace SistGurkas.Models.Comercial
{
    public class HistorialCliente
    {
        public int IdHistorial
        {
            get; set;
        }
        public int IdUnidad
        {
            get; set;
        }
        public DateTime FechaHora
        {
            get; set;
        }
        public string Usuario { get; set; } = "Sistema"; public string Evento { get; set; } = ""; public string? Detalle
        {
            get; set;
        }
    }
}