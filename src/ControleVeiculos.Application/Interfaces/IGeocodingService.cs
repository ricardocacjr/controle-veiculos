namespace ControleVeiculos.Application.Interfaces;

public interface IGeocodingService
{
    /// <summary>
    /// Resolve coordenadas pra um endereço legível. Retorna null se o serviço estiver
    /// indisponível ou não conseguir resolver — sempre uma sugestão pra conferência humana.
    /// </summary>
    Task<string?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);
}
