using SistGurkas.Models.Comercial;
namespace SistGurkas.Services.OpenAI;

public interface IOpenAiTdrService
{
    Task<AnalisisTdrIa> AnalizarPdfAsync(string rutaPdf, CancellationToken ct = default);
    Task<AnalisisTdrIa> CompletarPenalidadesYSedesAsync(string rutaPdf, CancellationToken ct = default);
    Task<RespuestaTdrIa> PreguntarSobreAnalisisAsync(string jsonAnalisis, string pregunta, CancellationToken ct = default);
}
