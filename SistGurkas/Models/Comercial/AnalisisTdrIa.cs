namespace SistGurkas.Models.Comercial;

public sealed class AnalisisTdrIa
{
    public string ResumenEjecutivo { get; set; } = "";
    public int PersonalTotal
    {
        get; set;
    }
    public int Armados
    {
        get; set;
    }
    public int Simples
    {
        get; set;
    }
    public int SedesDetectadas
    {
        get; set;
    }
    public List<PersonalTdrIa> Personal { get; set; } = [];
    public List<ItemTdrIa> Requisitos { get; set; } = [];
    public List<ItemTdrIa> Recursos { get; set; } = [];
    public List<PenalidadTdrIa> Penalidades { get; set; } = [];
    public List<SedeTdrIa> Sedes { get; set; } = [];
    public bool ComplementoContractualProcesado
    {
        get; set;
    }
}
public sealed class PersonalTdrIa
{
    public string PuestoPerfil { get; set; } = "";
    public int Cantidad
    {
        get; set;
    }
    public string Modalidad { get; set; } = "";
    public string Horario { get; set; } = "";
    public string Fuente { get; set; } = "";
}
public sealed class ItemTdrIa
{
    public string Descripcion { get; set; } = "";
    public string Fuente { get; set; } = "";
}

public sealed class PenalidadTdrIa
{
    public string Incumplimiento { get; set; } = "";
    public string Penalidad { get; set; } = "";
    public string Calculo { get; set; } = "";
    public string Frecuencia { get; set; } = "";
    public string Fuente { get; set; } = "";
}

public sealed class SedeTdrIa
{
    public string Nombre { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Distrito { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string Departamento { get; set; } = "";
    public int CantidadPersonal
    {
        get; set;
    }
    public int Armados
    {
        get; set;
    }
    public int Simples
    {
        get; set;
    }
    public string Horario { get; set; } = "";
    public string Contacto { get; set; } = "";
    public string Celular { get; set; } = "";
    public string Correo { get; set; } = "";
    public string CentroCosto { get; set; } = "";
    public string Fuente { get; set; } = "";
}

public sealed class RespuestaTdrIa
{
    public string Respuesta { get; set; } = "";
    public string Fuente { get; set; } = "";
    public bool TieneEvidencia
    {
        get; set;
    }
}
