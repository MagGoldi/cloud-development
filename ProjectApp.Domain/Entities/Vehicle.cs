namespace ProjectApp.Domain.Entities;

/// <summary>
/// Сущность транспортного средства
/// </summary>
public class Vehicle
{
    /// <summary>Уникальный идентификатор транспортного средства в системе</summary>
    public required int Id { get; set; }

    /// <summary>VIN-номер транспортного средства</summary>
    public required string Vin { get; set; }

    /// <summary>Производитель транспортного средства</summary>
    public required string Brand { get; set; }

    /// <summary>Модель транспортного средства</summary>
    public required string Model { get; set; }

    /// <summary>Год выпуска транспортного средства</summary>
    public int Year { get; set; }

    /// <summary>Тип корпуса (кузова)</summary>
    public required string BodyType { get; set; }

    /// <summary>Тип используемого топлива</summary>
    public required string FuelType { get; set; }

    /// <summary>Цвет корпуса</summary>
    public required string Color { get; set; }

    /// <summary>Пробег в километрах</summary>
    public double Mileage { get; set; }

    /// <summary>Дата последнего техобслуживания</summary>
    public DateOnly LastServiceDate { get; set; }
}
