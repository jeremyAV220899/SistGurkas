namespace SistGurkas.Models.Comercial;

public sealed class TdrDashboardVm
{
    public int TotalTdr
    {
        get; set;
    }
    public int TotalRequerimientos
    {
        get; set;
    }
    public int Personal
    {
        get; set;
    }
    public int Equipos
    {
        get; set;
    }
    public int Documentacion
    {
        get; set;
    }
    public int Activos
    {
        get; set;
    }
    public int Inactivos
    {
        get; set;
    }
    public decimal CumplimientoGeneral
    {
        get; set;
    }
    public int Cumplidos
    {
        get; set;
    }
    public int EnProceso
    {
        get; set;
    }
    public int Pendientes
    {
        get; set;
    }
    public int NoAplica
    {
        get; set;
    }
    public List<TdrCumplimientoResumenItem> CumplimientoPorTipo { get; set; } = [];
    public List<TdrCumplimientoResumenItem> CumplimientoPorSede { get; set; } = [];
    public List<TdrPendienteAreaItem> PendientesPorArea { get; set; } = [];
    public List<TdrEvolucionItem> Evolucion { get; set; } = [];
    public List<TdrRequerimientoItem> Items { get; set; } = [];
    public List<TdrResumenItem> PorCliente { get; set; } = [];
    public List<TdrResumenItem> PorSede { get; set; } = [];
    public List<TdrResumenItem> PorArea { get; set; } = [];
    public List<string> Clientes { get; set; } = [];
    public List<string> Contratos { get; set; } = [];
    public List<string> Sedes { get; set; } = [];
    public string? FiltroCliente
    {
        get; set;
    }
    public string? FiltroContrato
    {
        get; set;
    }
    public string? FiltroSede
    {
        get; set;
    }
    public string? FiltroCategoria
    {
        get; set;
    }
    public int? FiltroEstado
    {
        get; set;
    }
}

public sealed class TdrResumenItem
{
    public string Nombre { get; set; } = "";
    public int Total
    {
        get; set;
    }
    public int Personal
    {
        get; set;
    }
    public int Equipos
    {
        get; set;
    }
    public int Documentacion
    {
        get; set;
    }
    public double Porcentaje
    {
        get; set;
    }
}

public sealed class TdrRequerimientoItem
{
    public int IdTdrRequerimiento
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
    public int? IdDocumento
    {
        get; set;
    }
    public string Categoria { get; set; } = "";
    public string Subcategoria { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int Cantidad
    {
        get; set;
    }
    public string UnidadMedida { get; set; } = "";
    public string Modalidad { get; set; } = "";
    public string Horario { get; set; } = "";
    public string Dias { get; set; } = "";
    public string Especificacion { get; set; } = "";
    public string Frecuencia { get; set; } = "";
    public string AreaResponsable { get; set; } = "";
    public string Fuente { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Contrato { get; set; } = "";
    public string Sede { get; set; } = "";
    public int IdEstado { get; set; } = 1;
}

public sealed class TdrCumplimientoResumenItem
{
    public string Nombre { get; set; } = "";
    public int Total
    {
        get; set;
    }
    public decimal Porcentaje
    {
        get; set;
    }
}

public sealed class TdrPendienteAreaItem
{
    public string Area { get; set; } = "";
    public int Pendientes
    {
        get; set;
    }
}

public sealed class TdrEvolucionItem
{
    public string Periodo { get; set; } = "";
    public decimal Porcentaje
    {
        get; set;
    }
}
