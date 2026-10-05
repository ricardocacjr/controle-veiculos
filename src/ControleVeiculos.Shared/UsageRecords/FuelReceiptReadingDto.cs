namespace ControleVeiculos.Shared.UsageRecords;

/// <param name="ComprovanteId">Foto do cupom já guardada (anexo) — vai junto ao salvar o abastecimento.</param>
public record FuelReceiptReadingDto(decimal? Litros, decimal? ValorTotal, decimal? ValorPorLitro, Guid? ComprovanteId = null);
