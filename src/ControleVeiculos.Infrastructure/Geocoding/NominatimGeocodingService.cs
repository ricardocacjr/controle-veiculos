using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ControleVeiculos.Application.Interfaces;

namespace ControleVeiculos.Infrastructure.Geocoding;

/// <summary>
/// Geocodificação reversa via Nominatim (OpenStreetMap) — gratuito, sem necessidade de conta ou
/// chave de API, diferente dos serviços de voz/imagem que usam Google Cloud. Único requisito da
/// <a href="https://operations.osmfoundation.org/policies/nominatim/">política de uso</a> é
/// identificar a aplicação via User-Agent, e não fazer mais de ~1 requisição/segundo (uso deste
/// app — um abastecimento/início de uso por vez — fica bem abaixo disso).
/// </summary>
public class NominatimGeocodingService(HttpClient http, ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    public async Task<string?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        try
        {
            var url = $"reverse?format=json&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                       $"&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&zoom=18&addressdetails=1";

            var response = await http.GetFromJsonAsync<NominatimResponse>(url, ct);
            if (response is null)
                return null;

            return FormatarEndereco(response.Address) ?? (string.IsNullOrWhiteSpace(response.DisplayName) ? null : response.DisplayName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao resolver endereço via Nominatim para ({Lat}, {Lon}).", latitude, longitude);
            return null;
        }
    }

    // O display_name vem com região, CEP e país ("... Paraná, Região Sul, 80215-202, Brasil") —
    // longo demais pra tela do celular. Monta no formato de endereço brasileiro:
    // "Avenida Paulista, 1640 - Morro dos Ingleses, São Paulo".
    internal static string? FormatarEndereco(NominatimAddress? a)
    {
        if (a is null || string.IsNullOrWhiteSpace(a.Road))
            return null;

        var rua = string.IsNullOrWhiteSpace(a.HouseNumber) ? a.Road : $"{a.Road}, {a.HouseNumber}";
        var cidade = a.City ?? a.Town ?? a.Village ?? a.Municipality;
        var local = string.Join(", ", new[] { a.Suburb, cidade }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return local.Length == 0 ? rua : $"{rua} - {local}";
    }

    private class NominatimResponse
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    internal class NominatimAddress
    {
        [JsonPropertyName("road")] public string? Road { get; set; }
        [JsonPropertyName("house_number")] public string? HouseNumber { get; set; }
        [JsonPropertyName("suburb")] public string? Suburb { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("town")] public string? Town { get; set; }
        [JsonPropertyName("village")] public string? Village { get; set; }
        [JsonPropertyName("municipality")] public string? Municipality { get; set; }
    }
}
