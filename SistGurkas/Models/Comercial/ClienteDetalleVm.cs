namespace SistGurkas.Models.Comercial
{
    public class ClienteDetalleVm
    {
        public Unidad Cliente { get; set; } = new(); public List<ContactoUnidad> Contactos { get; set; } = new(); public List<Sede> Sedes { get; set; } = new(); public List<ContratoCliente> Contratos { get; set; } = new(); public List<RequerimientoCliente> Requerimientos { get; set; } = new(); public List<DocumentoCliente> Documentos { get; set; } = new(); public List<HistorialCliente> Historial { get; set; } = new();
    }
}