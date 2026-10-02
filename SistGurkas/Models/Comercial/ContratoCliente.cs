namespace SistGurkas.Models.Comercial
{
    public class ContratoCliente
    {
        public int IdContrato
        {
            get; set;
        }
        public int IdUnidad
        {
            get; set;
        }
        public string NumeroContrato { get; set; } = ""; public string Descripcion { get; set; } = ""; public DateTime? FechaInicio
        {
            get; set;
        }
        public DateTime? FechaFin
        {
            get; set;
        }
        public decimal? Monto
        {
            get; set;
        }
        public string Moneda { get; set; } = "PEN"; public int IdEstado { get; set; } = 1; public DateTime FechaRegistro
        {
            get; set;
        }
    }
}