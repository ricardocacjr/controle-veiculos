using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class UsageRecordRepository(AppDbContext context) : RepositoryBase<UsageRecord>(context), IUsageRecordRepository
{
    public async Task<UsageRecord?> GetWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        await Set
            .Include(u => u.Veiculo)
            .Include(u => u.Motorista)
            .Include(u => u.Fotos)
            .Include(u => u.NotasDeVoz)
            .Include(u => u.Abastecimentos)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<UsageRecord>> ListByMotoristaAsync(Guid motoristaId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(u => u.MotoristaId == motoristaId)
            .OrderByDescending(u => u.IniciadoEm)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UsageRecord>> ListByVeiculoAsync(Guid veiculoId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(u => u.VeiculoId == veiculoId)
            .OrderByDescending(u => u.IniciadoEm)
            .ToListAsync(ct);

    public async Task<UsageRecord?> GetEmAndamentoByMotoristaAsync(Guid motoristaId, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(
            u => u.MotoristaId == motoristaId && u.Status == UsageRecordStatus.EmAndamento, ct);

    public async Task AddPhotoAsync(VehiclePhoto photo, CancellationToken ct = default) =>
        await Context.Set<VehiclePhoto>().AddAsync(photo, ct);

    public async Task AddVoiceNoteAsync(VoiceNote note, CancellationToken ct = default) =>
        await Context.Set<VoiceNote>().AddAsync(note, ct);

    public async Task AddFuelEntryAsync(FuelEntry entry, CancellationToken ct = default) =>
        await Context.Set<FuelEntry>().AddAsync(entry, ct);
}
