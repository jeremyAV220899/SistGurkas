namespace SistGurkas.Models.Comercial
{
    public class RequerimientoCliente
    {
        public int IdRequerimiento
        {
            get; set;
        }
        public int IdUnidad
        {
            get; set;
        }
        public int IdContrato
        {
            get; set;
        }
        public int IdSede
        {
            get; set;
        }
        public string PuestoPerfil { get; set; } = ""; public int Cantidad
        {
            get; set;
        }
        public string Modalidad { get; set; } = "Simple"; public string Sexo { get; set; } = "Indistinto"; public string? Horario
        {
            get; set;
        }
        public string? Dias
        {
            get; set;
        }
        public DateTime? FechaInicio
        {
            get; set;
        }
        public DateTime? FechaFin
        {
            get; set;
        }
        public string? RequisitosEspeciales
        {
            get; set;
        }
        public string? RecursosAsociados
        {
            get; set;
        }
        public string? Observacion
        {
            get; set;
        }
        public int IdEstado { get; set; } = 1; public string? NumeroContrato
        {
            get; set;
        }
        public string? NombreSede
        {
            get; set;
        }
    }
}