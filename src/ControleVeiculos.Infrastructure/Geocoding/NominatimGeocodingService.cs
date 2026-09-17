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
                       $"&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&zoom=18&addressdetails=0";

            var response = await http.GetFromJsonAsync<NominatimResponse>(url, ct);
            return string.IsNullOrWhiteSpace(response?.DisplayName) ? null : response.DisplayName;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao resolver endereço via Nominatim para ({Lat}, {Lon}).", latitude, longitude);
            return null;
        }
    }

    private class NominatimResponse
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }
}
