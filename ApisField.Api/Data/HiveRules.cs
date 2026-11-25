using System.Globalization;

namespace ApisField.Api.Data;

/// <summary>Uma condição de alerta ativa numa leitura: o tipo e a gravidade do <see cref="Alert"/> que gera.</summary>
public record Condition(string Type, string Severity, string Message);

/// <summary>Limiares do contrato (docs/CONTRATO.md, "Estado e alertas").</summary>
/// <remarks>
/// O estado da colmeia e os alertas saem das mesmas condições, para nunca se contradizerem
/// (uma colmeia em <c>danger</c> tem sempre um alerta <c>danger</c> e vice-versa).
/// </remarks>
public static class HiveRules
{
    public const double MaxTempInterior = 37;
    public const double MaxCo2 = 3500;
    public const int MinBattery = 20;
    public const int MinSignal = 30;

    /// <summary>Sem leituras há mais do que isto, a colmeia fica <c>offline</c>.</summary>
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromHours(2);

    // Mensagens em português de Portugal, com vírgula decimal ("37,2 °C").
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    /// <summary>As condições de alerta presentes na leitura <paramref name="s"/>.</summary>
    /// <remarks>O CO₂ só conta no modelo Pro, o único com esse sensor.</remarks>
    public static List<Condition> Conditions(string model, HiveSensors s)
    {
        var conditions = new List<Condition>();
        if (s.WaspDetected)
            conditions.Add(new("wasp", "danger", "Vespa asiática detetada junto à entrada."));
        if (s.TempInterior > MaxTempInterior)
            conditions.Add(new("temperature", "danger",
                string.Create(Pt, $"Temperatura interior de {s.TempInterior:0.0} °C, acima de {MaxTempInterior} °C.")));
        if (model == "pro" && s.Co2 > MaxCo2)
            conditions.Add(new("co2", "warning", string.Create(Pt, $"CO₂ a {s.Co2:0} ppm, acima de {MaxCo2} ppm.")));
        if (s.Battery < MinBattery)
            conditions.Add(new("battery", "warning", $"Bateria a {s.Battery} %, abaixo de {MinBattery} %."));
        if (s.Signal < MinSignal)
            conditions.Add(new("signal", "warning", $"Sinal a {s.Signal} %, abaixo de {MinSignal} %."));
        return conditions;
    }

    /// <summary>Estado da colmeia, pela ordem da tabela do contrato.</summary>
    /// <param name="conditions">Resultado de <see cref="Conditions"/>.</param>
    /// <param name="lastReading">Instante da última leitura. Com as leituras simuladas, é sempre o atual.</param>
    /// <param name="now">Instante atual (UTC).</param>
    public static string Status(List<Condition> conditions, DateTime lastReading, DateTime now) =>
        conditions.Any(c => c.Severity == "danger") ? "danger"
        : conditions.Count > 0 ? "warning"
        : now - lastReading > OfflineAfter ? "offline"
        : "healthy";
}
