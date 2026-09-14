namespace ControleVeiculos.Mobile.Services;

public static class AppConfig
{
    /// <summary>
    /// Endereço da Api. "10.0.2.2" é o alias do emulador Android pra "localhost" da máquina host;
    /// em dispositivo físico, troque pelo IP da máquina na rede local ou pela URL de produção
    /// (ex: https://controle-veiculos-api.onrender.com/).
    /// </summary>
    public const string ApiBaseUrl = "http://10.0.2.2:5044/";
}
