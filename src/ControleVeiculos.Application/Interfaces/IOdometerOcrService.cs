namespace ControleVeiculos.Application.Interfaces;

public interface IOdometerOcrService
{
    /// <summary>
    /// Tenta ler o valor do odômetro numa foto. Retorna null se o serviço não estiver
    /// configurado, não achar nenhum número plausível, ou a leitura falhar — é sempre uma
    /// sugestão pra conferência humana, nunca aplicada automaticamente sem revisão.
    /// </summary>
    /// <param name="kmReferencia">Último km conhecido do veículo. Quando informado, só aceita
    /// números a partir dele (descarta relógio, autonomia, velocímetro etc.).</param>
    Task<int?> ExtractOdometerAsync(Stream imageStream, int? kmReferencia, CancellationToken ct = default);
}
