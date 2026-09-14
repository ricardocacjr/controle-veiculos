using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Shared.Vehicles;

public record UpdateVehicleRequest(string Placa, string Marca, string Modelo, int Ano, string? Cor, int OdometroAtual, VehicleStatus Status);
