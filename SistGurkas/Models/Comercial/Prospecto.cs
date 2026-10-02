namespace SistGurkas.Models.Comercial
{
    public class Prospecto
    {
        public int IdProspecto
        {
            get; set;
        }
        public string RazonSocial { get; set; } = string.Empty;
        public string? Ruc
        {
            get; set;
        }
        public string? NombreComercial
        {
            get; set;
        }
        public string? NombreContacto
        {
            get; set;
        }
        public string? CargoContacto
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
        public string? Direccion
        {
            get; set;
        }
        public string? Origen
        {
            get; set;
        }
        public string? Observacion
        {
            get; set;
        }
        public int? IdUsuarioResponsable
        {
            get; set;
        }
        public int IdEstado
        {
            get; set;
        }
        public string? NombreEstado
        {
            get; set;
        }
        public int? IdEstadoProspecto
        {
            get; set;
        }
        public string? NombreEstadoProspecto
        {
            get; set;
        }
        public DateTime FechaRegistro
        {
            get; set;
        }

        public string? TipoPersona
        {
            get; set;
        }
        public int? IdSector
        {
            get; set;
        }
        public int? IdActividad
        {
            get; set;
        }
        public string? PaginaWeb
        {
            get; set;
        }
        public int? IdDepartamento
        {
            get; set;
        }
        public int? IdProvincia
        {
            get; set;
        }
        public int? IdDistrito
        {
            get; set;
        }
        public string? Fuente
        {
            get; set;
        }
        public string? Prioridad
        {
            get; set;
        }
        public string? TamanoEstimado
        {
            get; set;
        }
        public decimal? ProbabilidadEstimada
        {
            get; set;
        }
        public string? TipoCliente
        {
            get; set;
        }
        public string? CompetenciaActual
        {
            get; set;
        }
        public DateTime? FechaEstimadaInicio
        {
            get; set;
        }
        public DateTime? ProximaGestion
        {
            get; set;
        }

        public List<ContactoProspecto> Contactos { get; set; } = new();
    }

    public class ContactoProspecto
    {
        public int IdContactoProspecto
        {
            get; set;
        }
        public int IdProspecto
        {
            get; set;
        }
        public string Nombre { get; set; } = string.Empty;
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
