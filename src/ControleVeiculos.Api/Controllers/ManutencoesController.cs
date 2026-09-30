using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Api.Relatorios;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Manutencoes;

namespace ControleVeiculos.Api.Controllers;

/// <summary>
/// Manutenções dos veículos no formato da nota da oficina (itens, mão de obra, total), com a nota
/// anexada e as próximas (vencida / em breve) sugeridas pelos itens.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
[Route("api/[controller]")]
public class ManutencoesController(
    IManutencaoRepository manutencaoRepository,
    IVehicleRepository vehicleRepository,
    IUsageRecordRepository usageRepository,
    ITextoDocumentoService textoDocumento) : ControllerBase
{
    private const long TamanhoMaximoNota = 10 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ManutencaoDto>>> Listar([FromQuery] Guid? veiculoId, CancellationToken ct) =>
        Ok((await manutencaoRepository.ListarAsync(veiculoId, ct)).Select(ToDto));

    /// <summary>Resumo por veículo: km atual, gasto e custo por km (12 meses), próximas manutenções.</summary>
    [HttpGet("resumo")]
    public async Task<ActionResult<IEnumerable<ManutencaoResumoDto>>> Resumo(CancellationToken ct)
    {
        var hoje = RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow);
        var veiculos = await vehicleRepository.ListAsync(ct);
        var manutencoes = await manutencaoRepository.ListarAsync(null, ct);
        var usos = await usageRepository.ListParaRelatorioAsync(RelatorioCalculadora.InicioDoDia(hoje.AddYears(-1)), ct);
        return Ok(veiculos.OrderBy(v => v.Placa).Select(v => ManutencaoCalculo.Resumo(v, manutencoes, usos, hoje)));
    }

    /// <summary>
    /// Recebe a nota da oficina (PDF ou foto), guarda como anexo e devolve o que conseguiu ler
    /// (data, km, itens, mão de obra, total). A tela mostra pra pessoa conferir antes de salvar.
    /// </summary>
    [HttpPost("ler-nota")]
    [Authorize(Roles = Roles.Admin)]
    [RequestSizeLimit(TamanhoMaximoNota + 1024 * 1024)]
    public async Task<ActionResult<LeituraNotaDto>> LerNota(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("Arquivo vazio.");
        if (file.Length > TamanhoMaximoNota)
            return BadRequest("Arquivo grande demais (máximo 10 MB).");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var conteudo = ms.ToArray();
        var ehPdf = conteudo.Length > 4 && conteudo[0] == '%' && conteudo[1] == 'P' && conteudo[2] == 'D' && conteudo[3] == 'F';

        var anexo = new ManutencaoAnexo
        {
            Nome = ehPdf ? "nota-oficina.pdf" : "nota-oficina.jpg",
            ContentType = ehPdf ? "application/pdf" : "image/jpeg",
            Conteudo = conteudo,
        };
        await manutencaoRepository.AdicionarAnexoAsync(anexo, ct);
        await manutencaoRepository.SaveChangesAsync(ct);

        var texto = await textoDocumento.ExtrairTextoAsync(conteudo, ehPdf, ct);
        var nota = string.IsNullOrWhiteSpace(texto) ? null : NotaOficinaParser.Interpretar(texto);
        return Ok(new LeituraNotaDto(
            anexo.Id, nota?.Data, nota?.Km, nota?.Placa, nota?.Oficina,
            nota?.Itens ?? [], nota?.MaoDeObra, nota?.Total, nota?.Observacao,
            nota is not null && (nota.Itens.Count > 0 || nota.Total is not null)));
    }

    /// <summary>A nota anexada (PDF ou foto), pra baixar/abrir.</summary>
    [HttpGet("{id:guid}/anexo")]
    public async Task<IActionResult> Anexo(Guid id, CancellationToken ct)
    {
        var manutencao = await manutencaoRepository.GetByIdAsync(id, ct);
        if (manutencao?.AnexoId is not { } anexoId || await manutencaoRepository.ObterAnexoAsync(anexoId, ct) is not { } anexo)
            return NotFound();
        return File(anexo.Conteudo, anexo.ContentType, $"{manutencao.Data:yyyy-MM-dd}-{anexo.Nome}");
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ManutencaoDto>> Criar(SalvarManutencaoRequest request, CancellationToken ct)
    {
        if (await Validar(request, ct) is { } erro)
            return BadRequest(erro);

        var manutencao = new Manutencao { VeiculoId = request.VeiculoId, Tipo = "" };
        Aplicar(manutencao, request);
        manutencao.Itens = Itens(manutencao.Id, request).ToList();
        manutencao.Proximas = Proximas(manutencao.Id, request).ToList();
        await manutencaoRepository.AddAsync(manutencao, ct);
        await AtualizarKmDoVeiculoAsync(request, ct);
        await manutencaoRepository.SaveChangesAsync(ct);
        return Ok(ToDto(manutencao));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ManutencaoDto>> Editar(Guid id, SalvarManutencaoRequest request, CancellationToken ct)
    {
        var manutencao = await manutencaoRepository.ObterParaEditarAsync(id, ct);
        if (manutencao is null)
            return NotFound();
        if (await Validar(request, ct) is { } erro)
            return BadRequest(erro);

        manutencao.VeiculoId = request.VeiculoId;
        Aplicar(manutencao, request);
        manutencaoRepository.SubstituirDetalhes(manutencao, Itens(manutencao.Id, request), Proximas(manutencao.Id, request));
        manutencao.UpdatedAt = DateTimeOffset.UtcNow;
        await AtualizarKmDoVeiculoAsync(request, ct);
        await manutencaoRepository.SaveChangesAsync(ct);
        return Ok(ToDto(manutencao));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var manutencao = await manutencaoRepository.GetByIdAsync(id, ct);
        if (manutencao is null)
            return NotFound();
        manutencaoRepository.Remove(manutencao);
        await manutencaoRepository.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> Validar(SalvarManutencaoRequest r, CancellationToken ct)
    {
        if (await vehicleRepository.GetByIdAsync(r.VeiculoId, ct) is null)
            return "Veículo não encontrado.";
        if (r.Km <= 0)
            return "Informe o km do carro na manutenção.";
        var itens = r.Itens ?? [];
        if (itens.Count == 0 && r.MaoDeObra <= 0)
            return "Inclua pelo menos um item (ou a mão de obra).";
        if (itens.Any(i => string.IsNullOrWhiteSpace(i.Descricao)))
            return "Todo item precisa de descrição.";
        if (r.MaoDeObra < 0 || itens.Any(i => i.Valor < 0 || i.Quantidade <= 0))
            return "Valores e quantidades não podem ser negativos.";
        foreach (var p in r.Proximas ?? [])
        {
            if (p.ProximaKm is { } pk && pk <= r.Km)
                return $"A próxima de {p.Tipo} precisa ser num km maior que o desta manutenção.";
            if (p.ProximaData is { } pd && pd <= r.Data)
                return $"A data da próxima de {p.Tipo} precisa ser depois desta manutenção.";
        }
        if (r.Data > RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow).AddDays(1))
            return "A data não pode ser no futuro.";
        return null;
    }

    private static void Aplicar(Manutencao m, SalvarManutencaoRequest r)
    {
        var itens = r.Itens ?? [];
        m.Data = r.Data;
        m.Km = r.Km;
        m.Tipo = CategoriasManutencao.Resumo(itens);
        m.Descricao = string.IsNullOrWhiteSpace(r.Observacao) ? null : r.Observacao.Trim();
        m.MaoDeObra = Math.Round(r.MaoDeObra, 2);
        m.Valor = Math.Round(itens.Sum(i => i.Valor) + r.MaoDeObra, 2);
        m.Oficina = string.IsNullOrWhiteSpace(r.Oficina) ? null : MotivoUso.Normalizar(r.Oficina);
        if (r.AnexoId is { } anexo)
            m.AnexoId = anexo;
    }

    private static IEnumerable<ManutencaoItem> Itens(Guid manutencaoId, SalvarManutencaoRequest r) =>
        (r.Itens ?? []).Select(i => new ManutencaoItem
        {
            ManutencaoId = manutencaoId,
            Quantidade = i.Quantidade,
            Descricao = i.Descricao.Trim(),
            Valor = Math.Round(i.Valor, 2),
        });

    private static IEnumerable<ManutencaoProxima> Proximas(Guid manutencaoId, SalvarManutencaoRequest r) =>
        (r.Proximas ?? [])
            .Where(p => p.ProximaKm is not null || p.ProximaData is not null)
            .Select(p => new ManutencaoProxima
            {
                ManutencaoId = manutencaoId,
                Tipo = MotivoUso.Normalizar(p.Tipo),
                ProximaKm = p.ProximaKm,
                ProximaData = p.ProximaData,
            });

    /// <summary>Km da manutenção maior que o km conhecido do carro → atualiza (referência pras próximas leituras).</summary>
    private async Task AtualizarKmDoVeiculoAsync(SalvarManutencaoRequest r, CancellationToken ct)
    {
        var veiculo = await vehicleRepository.GetByIdAsync(r.VeiculoId, ct);
        if (veiculo is not null && r.Km > veiculo.OdometroAtual)
        {
            veiculo.OdometroAtual = r.Km;
            vehicleRepository.Update(veiculo);
        }
    }

    private static ManutencaoDto ToDto(Manutencao m) => new(
        m.Id, m.VeiculoId, m.Veiculo?.Placa ?? "", m.Data, m.Km, m.Tipo, m.Descricao, m.Valor, m.MaoDeObra, m.Oficina,
        m.Itens.Select(i => new ManutencaoItemDto(i.Quantidade, i.Descricao, i.Valor)).ToList(),
        m.Proximas.Select(p => new ManutencaoProximaDto(p.Tipo, p.ProximaKm, p.ProximaData)).ToList(),
        m.AnexoId is not null);
}
