using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControleVeiculos.Domain.Enums;
using ControleVeiculos.Shared.Auth;
using ControleVeiculos.Shared.UsageRecords;
using ControleVeiculos.Shared.Vehicles;

namespace ControleVeiculos.Mobile.Services;

public class ControleVeiculosApiClient(HttpClient http, AuthStorage authStorage)
{
    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync()
    {
        using var request = AuthorizedRequest(HttpMethod.Get, "api/vehicles");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<VehicleDto>>() ?? [];
    }

    public async Task<UsageRecordDto?> GetUsoAtualAsync()
    {
        using var request = AuthorizedRequest(HttpMethod.Get, "api/usagerecords/atual");
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UsageRecordDto>();
    }

    public async Task<UsageRecordDetailDto?> GetUsoDetalheAsync(Guid id)
    {
        using var request = AuthorizedRequest(HttpMethod.Get, $"api/usagerecords/{id}");
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UsageRecordDetailDto>();
    }

    public async Task<UsageRecordDto?> IniciarUsoAsync(StartUsageRequest request)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, "api/usagerecords/iniciar");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UsageRecordDto>();
    }

    public async Task FinalizarUsoAsync(Guid usoId, int odometroFinal)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, $"api/usagerecords/{usoId}/finalizar");
        httpRequest.Content = JsonContent.Create(new FinishUsageRequest(odometroFinal));
        var response = await http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadFotoAsync(Guid usoId, Stream fileStream, string fileName, string contentType, VehiclePhotoType tipo, string? observacao)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, $"api/usagerecords/{usoId}/fotos");
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(tipo.ToString()), "tipo");
        if (observacao is not null)
            content.Add(new StringContent(observacao), "observacao");
        httpRequest.Content = content;

        var response = await http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadNotaDeVozAsync(Guid usoId, Stream fileStream, string fileName, string contentType)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, $"api/usagerecords/{usoId}/notas-de-voz");
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        httpRequest.Content = content;

        var response = await http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddAbastecimentoAsync(Guid usoId, AddFuelEntryRequest request)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, $"api/usagerecords/{usoId}/abastecimentos");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (authStorage.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authStorage.Token);
        return request;
    }
}
