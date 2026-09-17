namespace ControleVeiculos.Application.Interfaces;

public record FuelReceiptReading(decimal? Litros, decimal? ValorTotal, decimal? ValorPorLitro);

public interface IFuelReceiptOcrService
{
    /// <summary>
    /// Tenta ler litros/valor total/valor por litro de uma foto de nota fiscal ou cupom de
    /// abastecimento. Qualquer campo pode vir null se não for encontrado com confiança — sempre
    /// uma sugestão pra conferência humana, nunca aplicada sem revisão.
    /// </summary>
    Task<FuelReceiptReading> ExtractAsync(Stream imageStream, CancellationToken ct = default);
}
