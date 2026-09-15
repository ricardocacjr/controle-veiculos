using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControleVeiculos.Shared.Auth;
using ControleVeiculos.Shared.Drivers;
using ControleVeiculos.Shared.UsageRecords;
using ControleVeiculos.Shared.Vehicles;

namespace ControleVeiculos.Web.Services;

public class ControleVeiculosApiClient(HttpClient http, AuthState authState)
{
    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<AuthResponse?> RegisterAsync(string nomeCompleto, string email, string password, string role)
    {
        var response = await http.PostAsJsonAsync("api/auth/register", new RegisterRequest(nomeCompleto, email, password, role));
        await EnsureSuccessWithApiErrorAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync()
    {
        using var request = AuthorizedRequest(HttpMethod.Get, "api/vehicles");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<VehicleDto>>() ?? [];
    }

    public async Task<VehicleDto?> CreateVehicleAsync(CreateVehicleRequest vehicle)
    {
        using var request = AuthorizedRequest(HttpMethod.Post, "api/vehicles");
        request.Content = JsonContent.Create(vehicle);
        var response = await http.SendAsync(request);
        await EnsureSuccessWithApiErrorAsync(response);
        return await response.Content.ReadFromJsonAsync<VehicleDto>();
    }

    public async Task<VehicleDto?> UpdateVehicleAsync(Guid id, UpdateVehicleRequest vehicle)
    {
        using var request = AuthorizedRequest(HttpMethod.Put, $"api/vehicles/{id}");
        request.Content = JsonContent.Create(vehicle);
        var response = await http.SendAsync(request);
        await EnsureSuccessWithApiErrorAsync(response);
        return await response.Content.ReadFromJsonAsync<VehicleDto>();
    }

    public async Task DeleteVehicleAsync(Guid id)
    {
        using var request = AuthorizedRequest(HttpMethod.Delete, $"api/vehicles/{id}");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<DriverDto>> GetDriversAsync()
    {
        using var request = AuthorizedRequest(HttpMethod.Get, "api/drivers");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DriverDto>>() ?? [];
    }

    public async Task<DriverDto?> CreateDriverAsync(CreateDriverRequest driver)
    {
        using var request = AuthorizedRequest(HttpMethod.Post, "api/drivers");
        request.Content = JsonContent.Create(driver);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DriverDto>();
    }

    public async Task DeleteDriverAsync(Guid id)
    {
        using var request = AuthorizedRequest(HttpMethod.Delete, $"api/drivers/{id}");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<UsageRecordDto>> GetUsageRecordsAsync()
    {
        using var request = AuthorizedRequest(HttpMethod.Get, "api/usagerecords");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UsageRecordDto>>() ?? [];
    }

    public async Task<UsageRecordDetailDto?> GetUsageRecordDetailAsync(Guid id)
    {
        using var request = AuthorizedRequest(HttpMethod.Get, $"api/usagerecords/{id}");
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UsageRecordDetailDto>();
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

    public async Task<UsageRecordDto?> IniciarUsoAsync(StartUsageRequest request)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, "api/usagerecords/iniciar");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        await EnsureSuccessWithApiErrorAsync(response);
        return await response.Content.ReadFromJsonAsync<UsageRecordDto>();
    }

    public async Task FinalizarUsoAsync(Guid usoId, int odometroFinal)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, $"api/usagerecords/{usoId}/finalizar");
        httpRequest.Content = JsonContent.Create(new FinishUsageRequest(odometroFinal));
        var response = await http.SendAsync(httpRequest);
        await EnsureSuccessWithApiErrorAsync(response);
    }

    public async Task<VehiclePhotoDto?> UploadFotoAsync(Guid usoId, Stream fileStream, string fileName, string contentType, ControleVeiculos.Domain.Enums.VehiclePhotoType tipo, string? observacao)
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
        await EnsureSuccessWithApiErrorAsync(response);
        return await response.Content.ReadFromJsonAsync<VehiclePhotoDto>();
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
        await EnsureSuccessWithApiErrorAsync(response);
    }

    public async Task AddAbastecimentoAsync(Guid usoId, AddFuelEntryRequest request)
    {
        using var httpRequest = AuthorizedRequest(HttpMethod.Post, $"api/usagerecords/{usoId}/abastecimentos");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        await EnsureSuccessWithApiErrorAsync(response);
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (authState.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.Token);
        return request;
    }

    /// <summary>
    /// Como <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, mas em vez do genérico
    /// "Response status code does not indicate success: 400" propaga o erro que a Api devolve no
    /// corpo — uma lista de strings (ex: regras de senha do Identity) ou uma única string (ex:
    /// "Já existe um veículo com essa placa."), dependendo do endpoint. Sem isso as telas não
    /// tinham como mostrar pro usuário por que a operação falhou.
    /// </summary>
    private static async Task EnsureSuccessWithApiErrorAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        string? message = null;
        try
        {
            message = body.TrimStart().StartsWith('[')
                ? string.Join(" ", System.Text.Json.JsonSerializer.Deserialize<List<string>>(body) ?? [])
                : System.Text.Json.JsonSerializer.Deserialize<string>(body);
        }
        catch (System.Text.Json.JsonException)
        {
            // Corpo não era JSON de string/lista (ex: 401 sem corpo) — cai no fallback abaixo.
        }

        throw new HttpRequestException(string.IsNullOrWhiteSpace(message) ? $"Erro {(int)response.StatusCode}." : message);
    }
}
