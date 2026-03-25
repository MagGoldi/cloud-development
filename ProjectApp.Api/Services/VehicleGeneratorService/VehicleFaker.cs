using Bogus;
using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.VehicleGeneratorService;

public class VehicleFaker
{
    private static readonly string[] FuelTypes = ["Бензин", "Дизель", "Электро", "Гибрид", "Газ"];

    private static readonly Dictionary<string, string[]> BrandModels = new()
    {
        ["Toyota"]      = ["Camry", "Corolla", "RAV4", "Land Cruiser", "Yaris", "Highlander", "Prado"],
        ["BMW"]         = ["3 Series", "5 Series", "7 Series", "X3", "X5", "X6", "M4"],
        ["Mercedes"]    = ["C-Class", "E-Class", "S-Class", "GLE", "GLC", "A-Class", "CLA"],
        ["Volkswagen"]  = ["Passat", "Golf", "Tiguan", "Polo", "Touareg", "Jetta", "ID.4"],
        ["Hyundai"]     = ["Solaris", "Tucson", "Santa Fe", "Creta", "Elantra", "Sonata", "i30"],
        ["Ford"]        = ["Focus", "Mondeo", "Explorer", "Kuga", "Puma", "Mustang", "Ranger"],
        ["Kia"]         = ["Rio", "Sportage", "Sorento", "Ceed", "K5", "Seltos", "Stinger"],
        ["Audi"]        = ["A3", "A4", "A6", "Q3", "Q5", "Q7", "TT"],
        ["Nissan"]      = ["Qashqai", "X-Trail", "Altima", "Leaf", "Juke", "Murano", "Note"],
        ["Renault"]     = ["Logan", "Duster", "Sandero", "Megane", "Arkana", "Captur", "Laguna"],
        ["Lada"]        = ["Granta", "Vesta", "Largus", "Niva Travel", "XRAY", "4x4"],
        ["Skoda"]       = ["Octavia", "Superb", "Kodiaq", "Karoq", "Fabia", "Scala", "Kamiq"],
        ["Mazda"]       = ["Mazda3", "Mazda6", "CX-5", "CX-9", "MX-5", "CX-30"],
        ["Honda"]       = ["Civic", "Accord", "CR-V", "HR-V", "Jazz", "Pilot"],
        ["Subaru"]      = ["Outback", "Forester", "Impreza", "XV", "Legacy", "WRX"],
    };

    private const string RegistrationLetters = "АВЕКМНОРСТУХ";

    private readonly Faker<Vehicle> _faker;

    public VehicleFaker()
    {
        _faker = new Faker<Vehicle>("ru")
            .RuleFor(v => v.Id, f => f.IndexFaker + 1)
            .RuleFor(v => v.Brand, f => f.PickRandom(BrandModels.Keys.ToArray()))
            .RuleFor(v => v.Model, (f, v) => f.PickRandom(BrandModels[v.Brand]))
            .RuleFor(v => v.RegistrationNumber, f =>
            {
                var l1 = RegistrationLetters[f.Random.Int(0, RegistrationLetters.Length - 1)];
                var digits = f.Random.Int(100, 999);
                var l2 = RegistrationLetters[f.Random.Int(0, RegistrationLetters.Length - 1)];
                var l3 = RegistrationLetters[f.Random.Int(0, RegistrationLetters.Length - 1)];
                var region = f.Random.Int(10, 199);
                return $"{l1}{digits}{l2}{l3} {region}";
            })
            .RuleFor(v => v.OwnerName, f => f.Name.FullName())
            .RuleFor(v => v.Year, f => f.Random.Int(1984, 2026))
            .RuleFor(v => v.EngineVolume, f => Math.Round(f.Random.Decimal(0.8m, 6.0m), 1))
            .RuleFor(v => v.Mileage, f => f.Random.Int(0, 500_000))
            .RuleFor(v => v.FuelType, f => f.PickRandom(FuelTypes))
            .RuleFor(v => v.Price, f => Math.Round(f.Random.Decimal(100_000m, 10_000_000m), 0));
    }

    public Vehicle Generate() => _faker.Generate();
}
