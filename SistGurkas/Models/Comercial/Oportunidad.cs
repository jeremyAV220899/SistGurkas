using System;

namespace SistGurkas.Models.Comercial
{
    public class Oportunidad
    {
        public int IdOportunidad
        {
            get; set;
        }
        public int IdProspecto
        {
            get; set;
        }

        public string Nombre { get; set; } = "";

        public string? DescripcionNecesidad
        {
            get; set;
        }
        public string? ObjetivosAlcance
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

        public int IdEstadoOportunidad { get; set; } = 1;

        public int? ProbabilidadCierre
        {
            get; set;
        }

        public DateTime? FechaEstimadaCierre
        {
            get; set;
        }

        public decimal? MontoEstimado
        {
            get; set;
        }

        public string Moneda { get; set; } = "PEN";

        public string? TipoContratacion
        {
            get; set;
        }

        public string? Prioridad
        {
            get; set;
        }

        public DateTime? FechaAceptacion
        {
            get; set;
        }

        public DateTime FechaRegistro
        {
            get; set;
        }

        public int IdEstado { get; set; } = 1;

        // Datos auxiliares para mostrar en la vista
        public string? ProspectoNombre
        {
            get; set;
        }

        // Código automático de la oportunidad
        // Ejemplo: OP-2026-001
        public string CodigoOportunidad =>
            IdOportunidad > 0
                ? $"OP-{FechaRegistro:yyyy}-{IdOportunidad:D3}"
                : string.Empty;
    }
}