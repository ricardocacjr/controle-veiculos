using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Shared.Vehicles;

public record VehicleDto(
    Guid Id,
    string Placa,
    string Marca,
    string Modelo,
    int Ano,
    string? Cor,
    int OdometroAtual,
    VehicleStatus Status);
