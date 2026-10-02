namespace SistGurkas.Models.Comercial;

public sealed class PenalidadDashboardVm
{
    public decimal MontoTotal
    {
        get; set;
    }
    public int Registradas
    {
        get; set;
    }
    public int Anuladas
    {
        get; set;
    }
    public int EnAnalisis
    {
        get; set;
    }
    public int PendientesPago
    {
        get; set;
    }
    public int Pagadas
    {
        get; set;
    }
    public List<PenalidadRealItem> Penalidades { get; set; } = [];
    public List<PenalidadGraficoItem> PorMes { get; set; } = [];
    public List<PenalidadGraficoItem> PorCliente { get; set; } = [];
    public List<PenalidadGraficoItem> PorCategoria { get; set; } = [];
}
public sealed class PenalidadRealItem
{
    public int IdPenalidad
    {
        get; set;
    }
    public string Codigo { get; set; } = "";
    public DateTime Fecha
    {
        get; set;
    }
    public string NumeroDocumento { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Contrato { get; set; } = "";
    public string Sede { get; set; } = "";
    public string TipoPenalidad { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal MontoTotal
    {
        get; set;
    }
    public string Estado { get; set; } = "";
    public string Responsable { get; set; } = "";
    public DateTime? FechaLimitePago
    {
        get; set;
    }
}
public sealed class PenalidadGraficoItem
{
    public string Etiqueta { get; set; } = ""; public decimal Valor
    {
        get; set;
    }
    public int Cantidad
    {
        get; set;
    }
}
public sealed class PenalidadNuevaVm
{
    public List<Unidad> Clientes { get; set; } = [];
    public List<ContratoCliente> Contratos { get; set; } = [];
    public List<Sede> Sedes { get; set; } = [];
    public List<PenalidadClienteItem> Tipos { get; set; } = [];
}
