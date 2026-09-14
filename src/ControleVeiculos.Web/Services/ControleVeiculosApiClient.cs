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
        response.EnsureSuccessStatusCode();
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

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (authState.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.Token);
        return request;
    }

    /// <summary>
    /// Como <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, mas em vez do genérico
    /// "Response status code does not indicate success: 400" propaga a lista de erros que a Api
    /// devolve no corpo (ex: regras de senha do Identity) — sem isso a tela de cadastro não tinha
    /// como mostrar pro usuário por que falhou.
    /// </summary>
    private static async Task EnsureSuccessWithApiErrorAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        List<string>? errors = null;
        try
        {
            errors = await response.Content.ReadFromJsonAsync<List<string>>();
        }
        catch (System.Text.Json.JsonException)
        {
            // Corpo da resposta não era uma lista de erros (ex: 401 sem corpo) — cai no fallback abaixo.
        }

        throw new HttpRequestException(errors is { Count: > 0 } ? string.Join(" ", errors) : $"Erro {(int)response.StatusCode}.");
    }
}
