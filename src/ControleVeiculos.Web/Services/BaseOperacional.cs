namespace ControleVeiculos.Web.Services;

/// <summary>
/// Ponto de onde os veículos saem e pra onde voltam (seção "Base" do appsettings). Toda saída
/// começa e termina aqui — por isso não existe campo "destino": se o motorista está a menos de
/// <see cref="RaioMetros"/>, a origem vira "Base" sem precisar de endereço, e na chegada o app
/// confirma que ele voltou.
/// </summary>
public class BaseOperacional
{
    public string Nome { get; set; } = "Base";
    public string Endereco { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RaioMetros { get; set; } = 300;

    public bool Configurada => Latitude != 0 || Longitude != 0;

    public bool EstaNaBase(double latitude, double longitude) =>
        Configurada && DistanciaMetros(latitude, longitude) <= RaioMetros;

    public double DistanciaMetros(double latitude, double longitude)
    {
        const double raioTerra = 6_371_000;
        var dLat = Rad(latitude - Latitude);
        var dLon = Rad(longitude - Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(Latitude)) * Math.Cos(Rad(latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return raioTerra * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Rad(double graus) => graus * Math.PI / 180;
}
