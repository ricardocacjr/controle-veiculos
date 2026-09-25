using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace ControleVeiculos.Web.Services;

public record Posicao(double? Lat, double? Lng, string? Error)
{
    public bool Ok => Lat is not null && Lng is not null;
}

/// <summary>GPS do celular + endereço de reserva consultado pelo próprio celular (ver geolocation.js).</summary>
public class Localizacao(IJSRuntime js)
{
    public async Task<Posicao> ObterAsync()
    {
        try
        {
            return await js.InvokeAsync<Posicao?>("getGeolocation") ?? new Posicao(null, null, "Localização indisponível.");
        }
        catch (JSException ex)
        {
            return new Posicao(null, null, ex.Message);
        }
    }

    public async Task<string?> EnderecoAsync(double lat, double lng)
    {
        try
        {
            return await js.InvokeAsync<string?>("reverseGeocode", lat, lng);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public static async Task<MemoryStream> LerArquivoAsync(IBrowserFile arquivo, long limite = 20 * 1024 * 1024)
    {
        await using var stream = arquivo.OpenReadStream(maxAllowedSize: limite);
        var memoria = new MemoryStream();
        await stream.CopyToAsync(memoria);
        memoria.Position = 0;
        return memoria;
    }
}
