using System.ComponentModel.DataAnnotations;
using ApisField.Api.Data;

namespace ApisField.Api.Models;

// Modelos de entrada e saída das colmeias e dos alertas (docs/CONTRATO.md).

/// <summary>Corpo de POST /api/hives e de PUT /api/hives/{id}.</summary>
/// <remarks>
/// A latitude e a longitude são anuláveis só para se distinguir um campo em falta do valor por omissão (0),
/// que passaria como válido e poria a colmeia no golfo da Guiné.
/// </remarks>
public record HiveRequest(
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O nome tem de ter entre 2 e 60 caracteres.")]
    string? Name,

    [AllowedValues("starter", "pro", ErrorMessage = "O modelo tem de ser starter ou pro.")]
    string? Model,

    [Required(ErrorMessage = "A latitude é obrigatória.")]
    [Range(-90d, 90d, ErrorMessage = "A latitude tem de estar entre -90 e 90.")]
    double? Latitude,

    [Required(ErrorMessage = "A longitude é obrigatória.")]
    [Range(-180d, 180d, ErrorMessage = "A longitude tem de estar entre -180 e 180.")]
    double? Longitude,

    [StringLength(100, ErrorMessage = "A descrição do local tem no máximo 100 caracteres.")]
    string? Label)
{
    /// <summary>Passa os dados do pedido, já validados, para a entidade. Os valores dos sensores não mudam.</summary>
    public void ApplyTo(Hive hive)
    {
        hive.Name = Name!.Trim();
        hive.Model = Model!;
        hive.Latitude = Latitude!.Value;
        hive.Longitude = Longitude!.Value;
        hive.Label = Label?.Trim() ?? "";
    }
}

/// <summary>Localização da colmeia no formato do contrato.</summary>
public record HiveLocation(double Latitude, double Longitude, string Label);

/// <summary>Colmeia no formato do contrato, com a leitura e o estado do instante do pedido.</summary>
public record HiveResponse(int Id, string Name, string Model, string Status, HiveLocation Location,
    HiveSensors Sensors, DateTime LastUpdate, int OwnerId)
{
    /// <summary>Lê os sensores simulados no instante <paramref name="now"/> e calcula o estado.</summary>
    public static HiveResponse From(Hive h, DateTime now)
    {
        var sensors = HiveSimulator.Read(h, now);
        var status = HiveRules.Status(HiveRules.Conditions(h.Model, sensors), lastReading: now, now);
        return new(h.Id, h.Name, h.Model, status, new(h.Latitude, h.Longitude, h.Label), sensors, now, h.OwnerId);
    }
}

/// <summary>Valor de um sensor num dia. <see cref="Value"/> 0 quer dizer sem dados (contrato).</summary>
public record SensorReading(string Label, DateOnly Date, double Value);

/// <summary>Histórico dos últimos 7 dias, do mais antigo para hoje.</summary>
public record HiveHistory(SensorReading[] Temperature, SensorReading[] Weight, SensorReading[] Co2)
{
    private static readonly string[] WeekDays = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    /// <summary>Um valor por dia: a leitura do meio-dia (UTC) ou, hoje antes do meio-dia, a de agora.</summary>
    /// <remarks>
    /// A temperatura é a interior, a que diz como está a colónia. Os dias antes do registo da colmeia
    /// valem 0 (sem dados). O CO₂ vem vazio no modelo starter, que não tem esse sensor.
    /// </remarks>
    public static HiveHistory From(Hive hive, DateTime now)
    {
        var days = Enumerable.Range(-6, 7).Select(i => now.Date.AddDays(i)).ToArray();

        SensorReading[] Series(Func<HiveSensors, double?> sensor) =>
        [
            .. days.Select(day =>
            {
                var noon = day.AddHours(12);
                var value = day < hive.CreatedAt.Date ? 0 : sensor(HiveSimulator.Read(hive, noon < now ? noon : now)) ?? 0;
                return new SensorReading(WeekDays[(int)day.DayOfWeek], DateOnly.FromDateTime(day), value);
            }),
        ];

        return new(Series(s => s.TempInterior), Series(s => s.Weight), hive.Model == "pro" ? Series(s => s.Co2) : []);
    }
}

/// <summary>Alerta no formato do contrato.</summary>
public record AlertResponse(int Id, int HiveId, string HiveName, string Type, string Severity, string Message,
    DateTime Timestamp, bool Read)
{
    /// <summary>Exige a colmeia carregada (<c>Include</c>).</summary>
    public static AlertResponse From(Alert a) =>
        new(a.Id, a.HiveId, a.Hive!.Name, a.Type, a.Severity, a.Message, a.Timestamp, a.Read);
}
