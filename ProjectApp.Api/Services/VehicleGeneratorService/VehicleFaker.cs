using Bogus;
using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.VehicleGeneratorService;

public class VehicleFaker
{
    private static readonly string[] FuelTypes = ["Бензин", "Дизель", "Электро", "Гибрид", "Газ"];

    private static readonly string[] Brands =
    [
        "Toyota", "BMW", "Mercedes", "Volkswagen", "Hyundai",
        "Ford", "Kia", "Audi", "Nissan", "Renault"
    ];

    private static readonly string[] Trims =
    [
        "Comfort", "Sport", "Elite", "Plus", "Pro",
        "Active", "Max", "Base", "Premium", "Line"
    ];

    private const string RegistrationLetters = "АВЕКМНОРСТУХ";

    private readonly Faker<Vehicle> _faker;

    public VehicleFaker()
    {
        _faker = new Faker<Vehicle>("ru")
            .RuleFor(v => v.Id, f => f.IndexFaker + 1)
            .RuleFor(v => v.Brand, f => f.PickRandom(Brands))
            .RuleFor(v => v.Model, f => f.PickRandom(Trims))
            .RuleFor(v => v.RegistrationNumber, f =>
            {
                var letter1 = RegistrationLetters[f.Random.Int(0, RegistrationLetters.Length - 1)];
                var digits = f.Random.Int(100, 999);
                var letter2 = RegistrationLetters[f.Random.Int(0, RegistrationLetters.Length - 1)];
                var letter3 = RegistrationLetters[f.Random.Int(0, RegistrationLetters.Length - 1)];
                var region = f.Random.Int(10, 199);
                return $"{letter1}{digits}{letter2}{letter3} {region}";
            })
            .RuleFor(v => v.OwnerName, f => f.Name.FullName())
            .RuleFor(v => v.Year, f => f.Random.Int(1984, 2026))
            .RuleFor(v => v.EngineVolume, f => Math.Round(f.Random.Decimal(0.8m, 6.0m), 1))
            .RuleFor(v => v.Mileage, f => f.Random.Int(0, 500000))
            .RuleFor(v => v.FuelType, f => f.PickRandom(FuelTypes))
            .RuleFor(v => v.Price, f => Math.Round(f.Random.Decimal(100000m, 10000000m), 0));
    }

    public Vehicle Generate() => _faker.Generate();
}
