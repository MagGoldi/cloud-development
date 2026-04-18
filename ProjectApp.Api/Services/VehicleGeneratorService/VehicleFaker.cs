using Bogus;
using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.VehicleGeneratorService;

/// <summary>
/// Генератор тестовых данных транспортных средств на основе Bogus
/// </summary>
public class VehicleFaker
{
    private static readonly string[] _fuelTypes = ["Бензин", "Дизель", "Электро", "Гибрид", "Газ"];

    private static readonly string[] _bodyTypes = ["Седан", "Хэтчбек", "Универсал", "Кроссовер", "Внедорожник", "Купе", "Минивэн", "Пикап"];

    private static readonly Dictionary<string, string[]> _brandModels = new()
    {
        ["Toyota"]     = ["Camry", "Corolla", "RAV4", "Land Cruiser", "Yaris", "Highlander", "Prado"],
        ["BMW"]        = ["3 Series", "5 Series", "7 Series", "X3", "X5", "X6", "M4"],
        ["Mercedes"]   = ["C-Class", "E-Class", "S-Class", "GLE", "GLC", "A-Class", "CLA"],
        ["Volkswagen"] = ["Passat", "Golf", "Tiguan", "Polo", "Touareg", "Jetta", "ID.4"],
        ["Hyundai"]    = ["Solaris", "Tucson", "Santa Fe", "Creta", "Elantra", "Sonata", "i30"],
        ["Ford"]       = ["Focus", "Mondeo", "Explorer", "Kuga", "Puma", "Mustang", "Ranger"],
        ["Kia"]        = ["Rio", "Sportage", "Sorento", "Ceed", "K5", "Seltos", "Stinger"],
        ["Audi"]       = ["A3", "A4", "A6", "Q3", "Q5", "Q7", "TT"],
        ["Nissan"]     = ["Qashqai", "X-Trail", "Altima", "Leaf", "Juke", "Murano", "Note"],
        ["Renault"]    = ["Logan", "Duster", "Sandero", "Megane", "Arkana", "Captur", "Laguna"],
        ["Lada"]       = ["Granta", "Vesta", "Largus", "Niva Travel", "XRAY", "4x4"],
        ["Skoda"]      = ["Octavia", "Superb", "Kodiaq", "Karoq", "Fabia", "Scala", "Kamiq"],
        ["Mazda"]      = ["Mazda3", "Mazda6", "CX-5", "CX-9", "MX-5", "CX-30"],
        ["Honda"]      = ["Civic", "Accord", "CR-V", "HR-V", "Jazz", "Pilot"],
        ["Subaru"]     = ["Outback", "Forester", "Impreza", "XV", "Legacy", "WRX"],
    };

    private readonly Faker<Vehicle> _faker;

    public VehicleFaker()
    {
        var currentYear = DateTime.Now.Year;

        _faker = new Faker<Vehicle>("ru")
            .RuleFor(v => v.Id, f => f.IndexFaker + 1)
            .RuleFor(v => v.Vin, f => f.Vehicle.Vin())
            .RuleFor(v => v.Brand, f => f.PickRandom(_brandModels.Keys.ToArray()))
            .RuleFor(v => v.Model, (f, v) => f.PickRandom(_brandModels[v.Brand]))
            .RuleFor(v => v.Year, f => f.Random.Int(1984, currentYear))
            .RuleFor(v => v.BodyType, f => f.PickRandom(_bodyTypes))
            .RuleFor(v => v.FuelType, f => f.PickRandom(_fuelTypes))
            .RuleFor(v => v.Color, f => f.Commerce.Color())
            .RuleFor(v => v.Mileage, (f, v) => Math.Round(f.Random.Double(0, 500_000), 1))
            .RuleFor(v => v.LastServiceDate, (f, v) =>
                f.Date.BetweenDateOnly(new DateOnly(v.Year, 1, 1), DateOnly.FromDateTime(DateTime.Now)));
    }

    public Vehicle Generate() => _faker.Generate();
}
