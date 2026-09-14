namespace ControleVeiculos.Shared.Vehicles;

public record CreateVehicleRequest(string Placa, string Marca, string Modelo, int Ano, string? Cor, int OdometroAtual);
