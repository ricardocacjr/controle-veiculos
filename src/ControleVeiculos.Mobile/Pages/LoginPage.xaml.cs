using ControleVeiculos.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ControleVeiculos.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ControleVeiculosApiClient _api;
    private readonly AuthStorage _authStorage;
    private readonly IServiceProvider _services;

    public LoginPage(ControleVeiculosApiClient api, AuthStorage authStorage, IServiceProvider services)
    {
        InitializeComponent();
        _api = api;
        _authStorage = authStorage;
        _services = services;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        LoginButton.IsEnabled = false;

        try
        {
            var result = await _api.LoginAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            if (result is null)
            {
                ShowError("Não foi possível autenticar.");
                return;
            }

            await _authStorage.SetTokenAsync(result.Token);
            Application.Current!.MainPage = new NavigationPage(_services.GetRequiredService<HomePage>());
        }
        catch (HttpRequestException)
        {
            ShowError("E-mail ou senha inválidos.");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
