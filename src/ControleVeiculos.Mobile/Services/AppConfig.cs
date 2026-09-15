namespace ControleVeiculos.Mobile.Services;

public static class AppConfig
{
    /// <summary>
    /// Endereço da Api, escolhido pela plataforma em que o app está rodando:
    /// - Windows/MacCatalyst (roda na mesma máquina da Api em dev): localhost direto.
    /// - Android: "10.0.2.2" é o alias do emulador pra "localhost" da máquina host — em
    ///   dispositivo físico Android real, troque pelo IP da máquina na rede local.
    /// - iOS: o simulador consegue usar localhost direto; dispositivo físico precisa do IP da rede.
    /// Em produção, troque pela URL publicada (ex: https://controle-veiculos-api.onrender.com/).
    /// </summary>
    public static string ApiBaseUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5044/"
        : "http://localhost:5044/";
}
