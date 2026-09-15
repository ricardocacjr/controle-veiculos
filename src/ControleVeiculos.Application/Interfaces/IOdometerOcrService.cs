namespace ControleVeiculos.Application.Interfaces;

public interface IOdometerOcrService
{
    /// <summary>
    /// Tenta ler o valor do odômetro numa foto. Retorna null se o serviço não estiver
    /// configurado, não achar nenhum número na imagem, ou a leitura falhar — é sempre uma
    /// sugestão pra conferência humana, nunca aplicada automaticamente sem revisão.
    /// </summary>
    Task<int?> ExtractOdometerAsync(Stream imageStream, CancellationToken ct = default);
}
