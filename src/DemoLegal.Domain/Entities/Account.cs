using System;
using System.Globalization;

namespace DemoLegal.Domain.Entities;

/// <summary>
/// Лицевой счёт (ЛС) и справочные атрибуты из входного файла.
/// Чистая доменная модель — без EF/IO.
/// </summary>
public sealed class Account
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // Идентификаторы/основные поля
    public string Ls { get; init; } = string.Empty;          // "ЛС"
    public string? LsCode { get; init; }                     // "Код ЛС"
    public string? Fio { get; set; }                         // "ФИО" (может быть пусто)
    public string AddressRaw { get; init; } = string.Empty;  // "Адрес"
    /// <summary>Нормализованный адрес (JSON-строка с полями улица/дом/кв и т.п.).</summary>
    public string? AddressNormJson { get; set; }

    // Классификаторы
    public string? PremisesType { get; set; }                // "Тип помещения" (нормализованный текст)
    public string? LsStatus { get; set; }                    // "Статус ЛС" (Действующий/...)
    public DateOnly? LsCloseDate { get; set; }               // "Дата закрытия ЛС"
    public string? LsType { get; set; }                      // "Тип ЛС" (напр., "Распределенные")
    public string? MgmtStatus { get; set; }                  // "Статус управления домом"

    // Организационные привязки
    public string? Organization { get; set; }                // "Организация"
    public string? GroupCompany { get; set; }                // "ГК"
    public string? Division { get; set; }                    // "Дивизионы"
    public string? DivisionHead { get; set; }                // "Руководитель дивизиона"
    public string? AccrualCenter { get; set; }               // "Центр начислений"
    public string? ObjectName { get; set; }                  // "Объект"
    public string? District { get; set; }                    // "Район"
    public string? House { get; set; }                       // "Дом"
    public string? AdrN { get; set; }                        // "АдрН"
    public string? RoomNo { get; set; }                      // "№скв"

    /// <summary>
    /// Определение типа должника по "Тип ЛС":
    /// "Распределенные" → физлицо, иначе → юрлицо (застройщик).
    /// </summary>
    public DebtorType DetermineDebtorType()
    {
        var kind = (LsType ?? string.Empty).Trim();
        return string.Equals(kind, "Распределенные", StringComparison.OrdinalIgnoreCase)
            ? DebtorType.Person
            : DebtorType.Company;
    }

    /// <summary>Удобный формат адреса для документов: если есть нормализованный, берём его; иначе — сырой.</summary>
    public string DisplayAddress() => string.IsNullOrWhiteSpace(AddressNormJson) ? AddressRaw : AddressRaw;
    // (на старте возвращаем AddressRaw; позже можно распарсить JSON и собрать красивую строку)

    /// <summary>Признак активного ЛС (по полю "Статус ЛС").</summary>
    public bool IsActive() => string.Equals(LsStatus?.Trim(), "Действующий", StringComparison.OrdinalIgnoreCase);

    /// <summary>Фраза о статусе управления домом (текст для документов).</summary>
    public string BuildMgmtStatusText(DateOnly periodTo, string? contractEndDateFromExternal = null)
    {
        bool underLicense = (MgmtStatus ?? string.Empty).Contains("Управление (в лицензии)", StringComparison.OrdinalIgnoreCase);

        if (IsActive() && underLicense)
        {
            return $"Дом находится под управлением {Organization ?? "управляющей организации"} (в лицензии).";
        }

        // Выбираем лучшую дату "до какого числа управляли"
        var bestDate = contractEndDateFromExternal
            ?? (LsCloseDate.HasValue ? LsCloseDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                                     : new DateOnly(periodTo.Year, periodTo.Month, DateTime.DaysInMonth(periodTo.Year, periodTo.Month))
                                        .ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));

        return $"До {bestDate} управляющая организация {(Organization ?? "управляющая организация")} управляла домом.";
    }
}
