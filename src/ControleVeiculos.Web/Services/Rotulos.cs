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

    /// <summary>Horário de Brasília (UTC-3, sem horário de verão desde 2019). Fixo em vez de
    /// ToLocalTime porque o container do Render roda em UTC.</summary>
    public static DateTimeOffset Local(DateTimeOffset data) => data.ToOffset(TimeSpan.FromHours(-3));

    public static string DataHora(DateTimeOffset data) => Local(data).ToString("dd/MM/yyyy HH:mm");

    public static string Hora(DateTimeOffset data) => Local(data).ToString("HH:mm");

    public static string Km(int km) => $"{km:N0} km";

    public static string Duracao(DateTimeOffset inicio)
    {
        var d = DateTimeOffset.UtcNow - inicio;
        if (d.TotalMinutes < 1) return "agora";
        if (d.TotalHours < 1) return $"há {(int)d.TotalMinutes} min";
        if (d.TotalDays < 1) return $"há {(int)d.TotalHours}h{d.Minutes:00}";
        return $"há {(int)d.TotalDays} dia(s)";
    }

    public static string Iniciais(string? nome) =>
        string.Concat((nome ?? "?").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    /// <summary>Cor fixa por pessoa (mesmo nome = mesma cor) pro avatar sem foto.</summary>
    public static string CorAvatar(string? nome)
    {
        string[] cores = ["#2563eb", "#7c3aed", "#db2777", "#ea580c", "#16a34a", "#0891b2", "#ca8a04", "#4f46e5"];
        var soma = (nome ?? "").Sum(c => c);
        return cores[soma % cores.Length];
    }
}
