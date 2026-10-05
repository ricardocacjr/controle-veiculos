namespace ControleVeiculos.Application.Interfaces;

/// <param name="ValorTotal">Valor na bomba (antes do desconto).</param>
/// <param name="Desconto">Desconto do app do posto, quando o cupom/print mostra.</param>
public record FuelReceiptReading(decimal? Litros, decimal? ValorTotal, decimal? ValorPorLitro, decimal? Desconto = null);

public interface IFuelReceiptOcrService
{
    /// <summary>
    /// Tenta ler litros/valor total/valor por litro de uma foto de nota fiscal ou cupom de
    /// abastecimento. Qualquer campo pode vir null se não for encontrado com confiança — sempre
    /// uma sugestão pra conferência humana, nunca aplicada sem revisão.
    /// </summary>
    Task<FuelReceiptReading> ExtractAsync(Stream imageStream, CancellationToken ct = default);
}
