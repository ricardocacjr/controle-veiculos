namespace ControleVeiculos.Application.Interfaces;

/// <summary>Tira o texto de um documento: PDF (texto embutido) ou foto (OCR).</summary>
public interface ITextoDocumentoService
{
    Task<string?> ExtrairTextoAsync(byte[] conteudo, bool ehPdf, CancellationToken ct = default);
}
