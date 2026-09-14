using ControleVeiculos.Mobile.Services;
using ControleVeiculos.Shared.UsageRecords;
using ControleVeiculos.Shared.Vehicles;
using Microsoft.Extensions.DependencyInjection;

namespace ControleVeiculos.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly ControleVeiculosApiClient _api;
    private readonly AuthStorage _authStorage;
    private List<VehicleDto> _veiculos = [];
    private UsageRecordDto? _usoAtual;

    public HomePage(ControleVeiculosApiClient api, AuthStorage authStorage)
    {
        InitializeComponent();
        _api = api;
        _authStorage = authStorage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        EmAndamentoPanel.IsVisible = false;
        IniciarPanel.IsVisible = false;

        try
        {
            _usoAtual = await _api.GetUsoAtualAsync();
            if (_usoAtual is not null)
            {
                EmAndamentoPanel.IsVisible = true;
                return;
            }

            _veiculos = (await _api.GetVehiclesAsync())
                .Where(v => v.Status == ControleVeiculos.Domain.Enums.VehicleStatus.Disponivel)
                .ToList();
            VeiculoPicker.ItemsSource = _veiculos.Select(v => $"{v.Placa} — {v.Marca} {v.Modelo}").ToList();
            IniciarPanel.IsVisible = true;
        }
        catch (HttpRequestException)
        {
            await DisplayAlert("Erro", "Não foi possível carregar os dados.", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnContinuarClicked(object? sender, EventArgs e)
    {
        if (_usoAtual is not null)
            await Navigation.PushAsync(new TripPage(_api, _usoAtual.Id));
    }

    private async void OnIniciarClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        if (VeiculoPicker.SelectedIndex < 0)
        {
            ShowError("Selecione um veículo.");
            return;
        }
        if (string.IsNullOrWhiteSpace(FinalidadeEntry.Text))
        {
            ShowError("Informe a finalidade do uso.");
            return;
        }
        if (!int.TryParse(OdometroEntry.Text, out var odometro))
        {
            ShowError("Informe o odômetro inicial (número).");
            return;
        }

        IniciarButton.IsEnabled = false;
        try
        {
            var veiculo = _veiculos[VeiculoPicker.SelectedIndex];
            var request = new StartUsageRequest(veiculo.Id, FinalidadeEntry.Text, OrigemEntry.Text, DestinoEntry.Text, odometro);
            var uso = await _api.IniciarUsoAsync(request);
            if (uso is not null)
                await Navigation.PushAsync(new TripPage(_api, uso.Id));
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Não foi possível iniciar o uso: {ex.Message}");
        }
        finally
        {
            IniciarButton.IsEnabled = true;
        }
    }

    private void OnSairClicked(object? sender, EventArgs e)
    {
        _authStorage.SignOut();
        Application.Current!.MainPage = new NavigationPage(IPlatformApplication.Current!.Services.GetRequiredService<LoginPage>());
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
