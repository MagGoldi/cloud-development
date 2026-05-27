using System.Text.Json;

namespace ApiFunction.Services;

internal sealed class VehicleFaker
{
    private static readonly Random Rng = Random.Shared;

    private static readonly string[] Brands =
        ["Toyota", "BMW", "Mercedes", "Audi", "Ford", "Honda", "Volkswagen", "Hyundai", "Kia", "Nissan"];

    private static readonly string[] Models =
        ["Sedan", "SUV", "Hatchback", "Coupe", "Crossover", "Pickup", "Minivan", "Wagon"];

    private static readonly string[] BodyTypes =
        ["Sedan", "SUV", "Hatchback", "Coupe", "Crossover"];

    private static readonly string[] FuelTypes =
        ["Petrol", "Diesel", "Electric", "Hybrid"];

    private static readonly string[] Colors =
        ["White", "Black", "Silver", "Blue", "Red", "Grey", "Green"];

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Generate(int id)
    {
        var vehicle = new
        {
            id,
            vin = GenerateVin(),
            brand = Brands[Rng.Next(Brands.Length)],
            model = Models[Rng.Next(Models.Length)],
            year = Rng.Next(2010, 2025),
            bodyType = BodyTypes[Rng.Next(BodyTypes.Length)],
            fuelType = FuelTypes[Rng.Next(FuelTypes.Length)],
            color = Colors[Rng.Next(Colors.Length)],
            mileage = Math.Round(Rng.NextDouble() * 200000, 1),
            lastServiceDate = DateTime.UtcNow.AddDays(-Rng.Next(30, 730)).ToString("yyyy-MM-dd")
        };

        return JsonSerializer.Serialize(vehicle, CamelCase);
    }

    private static string GenerateVin()
    {
        const string chars = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";
        return string.Concat(Enumerable.Range(0, 17).Select(_ => chars[Rng.Next(chars.Length)]));
    }
}
