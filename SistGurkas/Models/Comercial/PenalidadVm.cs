namespace SistGurkas.Models.Comercial;

public sealed class TipoPenalidad
{
    public int IdTipoPenalidad
    {
        get; set;
    }
    public string Codigo { get; set; } = ""; public string Descripcion { get; set; } = ""; public string Categoria { get; set; } = ""; public string Gravedad { get; set; } = "Media"; public int IdEstado { get; set; } = 1;
}
public sealed class PenalidadClienteItem
{
    public int IdPenalidadCliente
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
    public int? IdTipoPenalidad
    {
        get; set;
    }
    public int? IdDocumento
    {
        get; set;
    }
    public string Descripcion { get; set; } = ""; public string Categoria { get; set; } = ""; public string FormaCalculo { get; set; } = ""; public decimal? Valor
    {
        get; set;
    }
    public string UnidadCalculo { get; set; } = ""; public string Frecuencia { get; set; } = ""; public string Fuente { get; set; } = ""; public string AreaResponsable { get; set; } = ""; public string Cliente { get; set; } = ""; public string Contrato { get; set; } = ""; public string Sede { get; set; } = ""; public int IdEstado { get; set; } = 1;
}
