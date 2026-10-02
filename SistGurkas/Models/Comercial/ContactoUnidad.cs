namespace SistGurkas.Models.Comercial
{
    public class ContactoUnidad
    {
        public int IdContactoUnidad
        {
            get; set;
        }
        public int IdUnidad
        {
            get; set;
        }
        public string Nombre { get; set; } = "";
        public string? Cargo
        {
            get; set;
        }
        public string? Telefono
        {
            get; set;
        }
        public string? Correo
        {
            get; set;
        }
        public bool EsPrincipal
        {
            get; set;
        }
        public string? Observacion
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