using ControleVeiculos.Mobile.Pages;
using ControleVeiculos.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ControleVeiculos.Mobile;

public partial class App : Application
{
    public App(IServiceProvider services, AuthStorage authStorage)
    {
        InitializeComponent();

        MainPage = new ContentPage();
        _ = InitializeAsync(services, authStorage);
    }

    private async Task InitializeAsync(IServiceProvider services, AuthStorage authStorage)
    {
        await authStorage.LoadAsync();

        MainPage = authStorage.IsAuthenticated
            ? new NavigationPage(services.GetRequiredService<HomePage>())
            : new NavigationPage(services.GetRequiredService<LoginPage>());
    }
}
