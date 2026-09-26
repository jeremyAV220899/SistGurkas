namespace SistGurkas.Models.Comercial
{
    public class ClienteDetalleVm
    {
        public Unidad Cliente { get; set; } = new(); public List<ContactoUnidad> Contactos { get; set; } = new(); public List<Sede> Sedes { get; set; } = new();
    }
}