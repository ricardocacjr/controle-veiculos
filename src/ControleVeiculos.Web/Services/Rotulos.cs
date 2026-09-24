using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Web.Services;

/// <summary>Textos e estilos de exibição dos enums (a tela nunca mostra o nome cru do enum).</summary>
public static class Rotulos
{
    public static (string Texto, string Chip) Uso(UsageRecordStatus status) => status switch
    {
        UsageRecordStatus.EmAndamento => ("Em uso", "chip-warning"),
        UsageRecordStatus.Finalizado => ("Finalizado", "chip-success"),
        UsageRecordStatus.Cancelado => ("Cancelado", "chip-danger"),
        _ => (status.ToString(), ""),
    };

    public static (string Texto, string Chip) Veiculo(VehicleStatus status) => status switch
    {
        VehicleStatus.Disponivel => ("Disponível", "chip-success"),
        VehicleStatus.EmUso => ("Em uso", "chip-warning"),
        VehicleStatus.Manutencao => ("Manutenção", "chip-danger"),
        VehicleStatus.Inativo => ("Inativo", ""),
        _ => (status.ToString(), ""),
    };

    public static string Foto(VehiclePhotoType tipo) => tipo switch
    {
        VehiclePhotoType.OdometroInicial => "Odômetro (saída)",
        VehiclePhotoType.OdometroFinal => "Odômetro",
        VehiclePhotoType.Avaria => "Avaria",
        VehiclePhotoType.ComprovanteAbastecimento => "Comprovante de abastecimento",
        _ => "Foto",
    };

    public static string DataHora(DateTimeOffset data) => data.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public static string Km(int km) => $"{km:N0} km";
}
