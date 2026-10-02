using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SistGurkas.Models.Comercial;

namespace SistGurkas.Services.OpenAI;

public sealed class OpenAiTdrService(HttpClient http, IConfiguration cfg) : IOpenAiTdrService
{
    private readonly string _key = cfg["OpenAI:ApiKey"] ?? "";
    private readonly string _model = cfg["OpenAI:ModelAnalisisTdr"] ?? "gpt-5.6-luna";
    private static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

    public async Task<AnalisisTdrIa> AnalizarPdfAsync(string rutaPdf, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_key))
            throw new InvalidOperationException("Falta configurar OpenAI:ApiKey.");
        if (!File.Exists(rutaPdf))
            throw new FileNotFoundException("No se encontró el TDR en el servidor.", rutaPdf);
        var fileId = await SubirArchivoAsync(rutaPdf, ct);
        var prompt = """Analiza este TDR de servicios de seguridad. Extrae SOLO información respaldada por el documento y NO inventes valores. PersonalTotal debe representar el total solicitado; Armados y Simples solo si son identificables. Extrae cada sede/instalación individualmente cuando el documento permita identificarla; conserva nombre y dirección literalmente y separa distrito/provincia/departamento solo si están expresos. SedesDetectadas debe ser coherente con las sedes distintas encontradas. Extrae también TODAS las penalidades o sanciones por incumplimiento: hecho que la genera, monto/porcentaje/UIT u otra penalidad, fórmula o forma de cálculo, frecuencia/unidad de aplicación y fuente. Si un campo no está indicado déjalo vacío o en cero. En Fuente incluye página, numeral o sección cuando sea posible. Devuelve exclusivamente JSON válido con esta forma: {"resumenEjecutivo":"","personalTotal":0,"armados":0,"simples":0,"sedesDetectadas":0,"personal":[{"puestoPerfil":"","cantidad":0,"modalidad":"","horario":"","fuente":""}],"requisitos":[{"descripcion":"","fuente":""}],"recursos":[{"descripcion":"","fuente":""}],"penalidades":[{"incumplimiento":"","penalidad":"","calculo":"","frecuencia":"","fuente":""}],"sedes":[{"nombre":"","direccion":"","distrito":"","provincia":"","departamento":"","cantidadPersonal":0,"armados":0,"simples":0,"horario":"","contacto":"","celular":"","correo":"","centroCosto":"","fuente":""}]}""";
        var body = new
        {
            model = _model,
            input = new object[] { new { role = "user", content = new object[] { new { type = "input_file", file_id = fileId }, new { type = "input_text", text = prompt } } } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var res = await http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("OpenAI: " + raw);
        using var doc = JsonDocument.Parse(raw);
        var text = ExtraerTexto(doc.RootElement);
        text = LimpiarJson(text);
        return JsonSerializer.Deserialize<AnalisisTdrIa>(text, J) ?? throw new InvalidOperationException("OpenAI no devolvió una ficha válida.");
    }

    public async Task<AnalisisTdrIa> CompletarPenalidadesYSedesAsync(string rutaPdf, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_key))
            throw new InvalidOperationException("Falta configurar OpenAI:ApiKey.");
        if (!File.Exists(rutaPdf))
            throw new FileNotFoundException("No se encontró el TDR en el servidor.", rutaPdf);

        var fileId = await SubirArchivoAsync(rutaPdf, ct);
        var prompt = """Revisa este TDR de servicios de seguridad y realiza una extracción COMPLEMENTARIA. No vuelvas a resumir personal, requisitos ni recursos generales. Extrae SOLO: (1) TODAS las penalidades/sanciones por incumplimiento, indicando incumplimiento, penalidad exacta (monto, porcentaje, UIT u otra), cálculo o fórmula, frecuencia/unidad de aplicación y fuente; (2) TODAS las sedes/instalaciones donde se prestará el servicio, indicando nombre, dirección literal, distrito, provincia, departamento, cantidad de personal, armados, simples, horario y fuente, únicamente cuando esos datos estén expresos. NO inventes ni deduzcas direcciones o ubigeos. Si un dato no figura, déjalo vacío o cero. En fuente incluye página, numeral o sección cuando sea posible. Devuelve exclusivamente JSON válido con esta forma: {"penalidades":[{"incumplimiento":"","penalidad":"","calculo":"","frecuencia":"","fuente":""}],"sedes":[{"nombre":"","direccion":"","distrito":"","provincia":"","departamento":"","cantidadPersonal":0,"armados":0,"simples":0,"horario":"","contacto":"","celular":"","correo":"","centroCosto":"","fuente":""}]}""";

        var body = new
        {
            model = _model,
            input = new object[] { new { role = "user", content = new object[] { new { type = "input_file", file_id = fileId }, new { type = "input_text", text = prompt } } } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var res = await http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("OpenAI: " + raw);
        using var doc = JsonDocument.Parse(raw);
        var text = LimpiarJson(ExtraerTexto(doc.RootElement));
        return JsonSerializer.Deserialize<AnalisisTdrIa>(text, J) ?? throw new InvalidOperationException("OpenAI no devolvió un complemento válido.");
    }

    public async Task<RespuestaTdrIa> PreguntarSobreAnalisisAsync(
    string jsonAnalisis,
    string pregunta,
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_key))
            throw new InvalidOperationException("Falta configurar OpenAI:ApiKey.");

        if (string.IsNullOrWhiteSpace(jsonAnalisis))
            throw new InvalidOperationException("El TDR no tiene un análisis almacenado.");

        if (string.IsNullOrWhiteSpace(pregunta))
            throw new ArgumentException("Escribe una pregunta.", nameof(pregunta));

        var prompt = $$"""
Eres un asistente que responde preguntas sobre un TDR de servicios de seguridad.

Usa EXCLUSIVAMENTE la ficha estructurada ya extraída que aparece abajo.
No inventes ni completes información utilizando conocimiento externo.

Si la ficha no contiene evidencia suficiente para responder:
- Indica claramente que la información no fue encontrada en el análisis almacenado.
- Sugiere revisar el TDR original.
- No supongas la respuesta.

Cuando exista una fuente, página, numeral o sección en la ficha,
inclúyela en el campo "fuente".

Devuelve exclusivamente JSON válido con esta estructura:

{
  "respuesta": "",
  "fuente": "",
  "tieneEvidencia": true
}

FICHA ESTRUCTURADA:
{{jsonAnalisis}}

PREGUNTA:
{{pregunta}}
""";

        var body = new
        {
            model = _model,
            input = prompt
        };

        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/responses");

        req.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _key);

        req.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var res = await http.SendAsync(req, ct);

        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("OpenAI: " + raw);

        using var doc = JsonDocument.Parse(raw);

        var text = LimpiarJson(
            ExtraerTexto(doc.RootElement)
        );

        return JsonSerializer.Deserialize<RespuestaTdrIa>(text, J)
            ?? throw new InvalidOperationException(
                "OpenAI no devolvió una respuesta válida.");
    }

    private async Task<string> SubirArchivoAsync(string path, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("user_data"), "purpose");
        await using var fs = File.OpenRead(path);
        using var fc = new StreamContent(fs);
        fc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fc, "file", Path.GetFileName(path));
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/files");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        req.Content = form;
        using var res = await http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo subir el PDF a OpenAI: " + raw);
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("OpenAI no devolvió file_id.");
    }
    private static string ExtraerTexto(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var ot) && ot.ValueKind == JsonValueKind.String)
            return ot.GetString() ?? "";
        if (root.TryGetProperty("output", out var output))
        foreach (var o in output.EnumerateArray())
        if (o.TryGetProperty("content", out var c))
        foreach (var x in c.EnumerateArray())
        if (x.TryGetProperty("text", out var t))
            return t.GetString() ?? "";
        throw new InvalidOperationException("La respuesta de OpenAI no contiene texto utilizable.");
    }
    private static string LimpiarJson(string s)
    {
        s = s.Trim();
        if (s.StartsWith("```"))
        {
            var p = s.IndexOf('\n');
            if (p >= 0)
                s = s[(p + 1)..];
            if (s.EndsWith("```"))
                s = s[..^3];
        }
        return s.Trim();
    }
}
