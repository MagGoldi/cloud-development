namespace ProjectApp.Domain.Entities;

/// <summary>
/// Сущность транспортного средства
/// </summary>
public class Vehicle
{
    /// <summary>Уникальный идентификатор транспортного средства в системе</summary>
    public required int Id { get; set; }

    /// <summary>Марка (производитель) транспортного средства</summary>
    public required string Brand { get; set; }

    /// <summary>Модель (комплектация) транспортного средства</summary>
    public required string Model { get; set; }

    /// <summary>Государственный регистрационный номер</summary>
    public required string RegistrationNumber { get; set; }

    /// <summary>Полное имя владельца транспортного средства</summary>
    public required string OwnerName { get; set; }

    /// <summary>Год выпуска транспортного средства</summary>
    public int Year { get; set; }

    /// <summary>Объём двигателя в литрах</summary>
    public decimal EngineVolume { get; set; }

    /// <summary>Пробег в километрах</summary>
    public int Mileage { get; set; }

    /// <summary>Тип используемого топлива</summary>
    public required string FuelType { get; set; }

    /// <summary>Рыночная стоимость транспортного средства в рублях</summary>
    public decimal Price { get; set; }
}
