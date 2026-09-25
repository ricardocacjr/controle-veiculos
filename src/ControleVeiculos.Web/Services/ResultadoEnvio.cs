using System.Text.Json;

namespace ControleVeiculos.Web.Services;

/// <summary>Resultado de um envio feito direto pelo navegador pra Api (ver cvEnvio em app.js).</summary>
public record ResultadoEnvio(bool Ok, int Status, string? Corpo, double? Lat, double? Lng, string? GeoErro)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Posicao Posicao => new(Lat, Lng, GeoErro);

    public T? Ler<T>()
    {
        if (string.IsNullOrWhiteSpace(Corpo)) return default;
        try { return JsonSerializer.Deserialize<T>(Corpo, Json); }
        catch (JsonException) { return default; }
    }

    /// <summary>Mensagem pra mostrar quando <see cref="Ok"/> é false (a Api devolve string ou lista de strings).</summary>
    public string Mensagem
    {
        get
        {
            if (Status == 0) return Corpo ?? "Não consegui enviar. Tente de novo.";
            if (Status == 401) return "Sua sessão expirou. Saia e entre de novo.";
            if (Status is 502 or 503 or 504) return "O servidor está iniciando. Aguarde alguns segundos e tente de novo.";
            try
            {
                var texto = Corpo?.TrimStart() ?? "";
                if (texto.StartsWith('[')) return string.Join(" ", JsonSerializer.Deserialize<List<string>>(texto, Json) ?? []);
                if (texto.StartsWith('"')) return JsonSerializer.Deserialize<string>(texto, Json) ?? $"Erro {Status}.";
            }
            catch (JsonException) { }
            return $"Erro {Status}. Tente de novo.";
        }
    }
}
