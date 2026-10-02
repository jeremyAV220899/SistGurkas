namespace SistGurkas.Models.Comercial;

public sealed class TdrCumplimiento
{
    public int IdTdrRequerimiento
    {
        get; set;
    }
    public decimal CantidadRequerida
    {
        get; set;
    }
    public decimal CantidadCumplida
    {
        get; set;
    }
    public decimal PorcentajeCumplimiento
    {
        get; set;
    }
    public string EstadoCumplimiento { get; set; } = "PENDIENTE";
    public string? Observacion
    {
        get; set;
    }
    public string? EvidenciaRuta
    {
        get; set;
    }
    public string? EvidenciaNombre
    {
        get; set;
    }
    public DateTime? FechaPrimeraGestion
    {
        get; set;
    }
    public DateTime FechaUltimaActualizacion
    {
        get; set;
    }
}

public sealed class TdrRegistrarAvanceRequest
{
    public int IdTdrRequerimiento
    {
        get; set;
    }
    public decimal? CantidadCumplida
    {
        get; set;
    }
    public decimal? PorcentajeManual
    {
        get; set;
    }
    public string? EstadoSolicitado
    {
        get; set;
    }
    public string? Observacion
    {
        get; set;
    }
}
