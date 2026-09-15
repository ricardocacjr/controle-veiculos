using ControleVeiculos.Domain.Enums;
using ControleVeiculos.Mobile.Services;
using ControleVeiculos.Shared.UsageRecords;
using Plugin.Maui.Audio;

namespace ControleVeiculos.Mobile.Pages;

public partial class TripPage : ContentPage
{
    private readonly ControleVeiculosApiClient _api;
    private readonly Guid _usoId;
    private readonly IAudioRecorder _recorder = AudioManager.Current.CreateRecorder();

    public TripPage(ControleVeiculosApiClient api, Guid usoId)
    {
        InitializeComponent();
        _api = api;
        _usoId = usoId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadResumoAsync();
    }

    private async Task LoadResumoAsync()
    {
        var uso = await _api.GetUsoDetalheAsync(_usoId);
        if (uso is not null)
            ResumoLabel.Text = $"{uso.VeiculoPlaca} — {uso.Finalidade}\nOdômetro inicial: {uso.OdometroInicial} km";
    }

    private async void OnFotoOdometroClicked(object? sender, EventArgs e) => await CapturarFotoAsync(VehiclePhotoType.OdometroFinal, "Odômetro");
    private async void OnFotoAvariaClicked(object? sender, EventArgs e) => await CapturarFotoAsync(VehiclePhotoType.Avaria, "Avaria");

    private async Task CapturarFotoAsync(VehiclePhotoType tipo, string observacao)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                ShowError("Este dispositivo não suporta captura de foto.");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null)
                return;

            SetLoading(true);
            await using var stream = await photo.OpenReadAsync();
            var foto = await _api.UploadFotoAsync(_usoId, stream, photo.FileName, "image/jpeg", tipo, observacao);
            ShowStatus(foto?.OdometroLido is { } lido
                ? $"Foto enviada. Leitura sugerida do odômetro: {lido} km — confira antes de usar."
                : "Foto enviada.");
        }
        catch (FeatureNotSupportedException)
        {
            ShowError("Câmera não disponível neste dispositivo.");
        }
        catch (PermissionException)
        {
            ShowError("Permissão de câmera negada. Habilite nas configurações do dispositivo.");
        }
        catch (HttpRequestException)
        {
            ShowError("Não foi possível enviar a foto.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnGravarClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_recorder.IsRecording)
            {
                var audioSource = await _recorder.StopAsync();
                GravandoLabel.IsVisible = false;
                GravarButton.Text = "🎙️ Gravar nota de voz";

                SetLoading(true);
                await using var stream = audioSource.GetAudioStream();
                await _api.UploadNotaDeVozAsync(_usoId, stream, $"{Guid.NewGuid()}.wav", "audio/wav");
                ShowStatus("Nota de voz enviada — transcrição pode levar alguns segundos.");
                return;
            }

            var micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            if (micStatus != PermissionStatus.Granted)
            {
                ShowError("Permissão de microfone negada.");
                return;
            }

            await _recorder.StartAsync();
            GravandoLabel.IsVisible = true;
            GravarButton.Text = "⏹️ Parar gravação";
        }
        catch (HttpRequestException)
        {
            ShowError("Não foi possível enviar a nota de voz.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnAbastecimentoClicked(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(LitrosEntry.Text, out var litros) ||
            !decimal.TryParse(ValorEntry.Text, out var valor) ||
            !int.TryParse(OdometroAbastecimentoEntry.Text, out var odometro))
        {
            ShowError("Preencha litros, valor e odômetro corretamente.");
            return;
        }

        SetLoading(true);
        try
        {
            await _api.AddAbastecimentoAsync(_usoId, new AddFuelEntryRequest(litros, valor, odometro));
            ShowStatus("Abastecimento registrado.");
            LitrosEntry.Text = ValorEntry.Text = OdometroAbastecimentoEntry.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            ShowError("Não foi possível registrar o abastecimento.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnFinalizarClicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(OdometroFinalEntry.Text, out var odometroFinal))
        {
            ShowError("Informe o odômetro final (número).");
            return;
        }

        var confirmar = await DisplayAlert("Finalizar uso", "Confirma o encerramento deste uso do veículo?", "Sim", "Cancelar");
        if (!confirmar)
            return;

        SetLoading(true);
        try
        {
            await _api.FinalizarUsoAsync(_usoId, odometroFinal);
            await Navigation.PopAsync();
        }
        catch (HttpRequestException)
        {
            ShowError("Não foi possível finalizar o uso.");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsVisible = loading;
        LoadingIndicator.IsRunning = loading;
    }

    private void ShowStatus(string message)
    {
        ErrorLabel.IsVisible = false;
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
    }

    private void ShowError(string message)
    {
        StatusLabel.IsVisible = false;
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
