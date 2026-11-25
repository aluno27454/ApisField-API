namespace ApisField.Api.Data;

/// <summary>Leitura dos sensores de uma colmeia, no formato do contrato (<c>HiveSensors</c>).</summary>
public record HiveSensors(
    double TempInterior, double TempExterior, double Humidity, double Weight, double? Co2,
    int Battery, int Signal, bool WaspDetected);

/// <summary>Simula as leituras dos sensores, porque nenhuma colmeia tem hardware real (contrato).</summary>
/// <remarks>
/// A simulação é determinística: a mesma colmeia no mesmo instante dá sempre a mesma leitura.
/// O id da colmeia é a semente, a hora do dia faz variar os valores ao longo do dia e cada dia tem um
/// pequeno desvio próprio, para o histórico não ser uma linha reta.
/// Os valores variam pouco em torno dos de referência guardados na colmeia, de propósito: uma colmeia
/// saudável não passa a alerta por causa da simulação, e as dos dados iniciais mantêm o estado com que
/// foram criadas (ver <see cref="Seed"/>).
/// </remarks>
public static class HiveSimulator
{
    /// <summary>CO₂ de referência de uma colmeia Pro sem valor próprio (ex.: uma starter mudada para Pro).</summary>
    public const double DefaultCo2 = 1500;

    /// <summary>Leitura da colmeia <paramref name="hive"/> no instante <paramref name="at"/> (UTC).</summary>
    public static HiveSensors Read(Hive hive, DateTime at)
    {
        // Até 2 horas de desfasamento, para as colmeias não variarem todas ao mesmo tempo.
        var phase = new Random(hive.Id).NextDouble() * 2;

        // Desvio do dia, entre −0,5 e 0,5: o mesmo o dia todo, diferente de dia para dia.
        var day = (int)(at.Date - DateTime.UnixEpoch).TotalDays;
        var shift = new Random(hive.Id * 10_007 + day).NextDouble() - 0.5;

        // Ciclo diário: 1 por volta das 15h (mais calor, abelhas fora a forragear), −1 por volta das 3h.
        var sun = Math.Sin((at.TimeOfDay.TotalHours - phase - 9) / 24 * 2 * Math.PI);

        return new HiveSensors(
            TempInterior: Round(hive.TempInterior + 0.3 * sun + 0.2 * shift),
            TempExterior: Round(hive.TempExterior + 5 * sun + 2 * shift),
            Humidity: Round(Math.Clamp(hive.Humidity - 8 * sun + 4 * shift, 0, 100)),
            Weight: Round(hive.Weight - 0.3 * Math.Max(sun, 0) + 0.4 * shift),
            // Mais CO₂ de noite, com as abelhas todas dentro.
            Co2: hive.Model == "pro" ? Math.Round((hive.Co2 ?? DefaultCo2) - 150 * sun + 50 * shift) : null,
            Battery: hive.Battery,
            Signal: Math.Clamp(hive.Signal + (int)Math.Round(4 * shift), 0, 100),
            WaspDetected: hive.WaspDetected);
    }

    private static double Round(double value) => Math.Round(value, 1);
}
