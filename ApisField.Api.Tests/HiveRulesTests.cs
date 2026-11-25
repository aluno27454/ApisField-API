using ApisField.Api.Data;

namespace ApisField.Api.Tests;

/// <summary>Testes unitários da simulação dos sensores e dos limiares do estado.</summary>
public class HiveRulesTests
{
    private static readonly DateTime Agora = new(2026, 11, 13, 10, 57, 0, DateTimeKind.Utc);

    private static Hive Colmeia(int id = 1, string model = "pro") => new() { Id = id, Name = "Teste", Model = model };

    // ---- Simulação ----

    [Fact]
    public void Simulacao_EDeterministica()
    {
        Assert.Equal(HiveSimulator.Read(Colmeia(), Agora), HiveSimulator.Read(Colmeia(), Agora));
    }

    [Fact]
    public void Simulacao_VariaComAHoraDoDia()
    {
        var madrugada = HiveSimulator.Read(Colmeia(), Agora.Date.AddHours(3));
        var tarde = HiveSimulator.Read(Colmeia(), Agora.Date.AddHours(15));

        Assert.True(tarde.TempExterior > madrugada.TempExterior + 5);
        Assert.True(madrugada.Co2 > tarde.Co2); // de noite as abelhas estão todas dentro
    }

    [Fact]
    public void Simulacao_ColmeiasDiferentes_TemLeiturasDiferentes()
    {
        Assert.NotEqual(HiveSimulator.Read(Colmeia(id: 1), Agora), HiveSimulator.Read(Colmeia(id: 2), Agora));
    }

    [Fact]
    public void Simulacao_Starter_NaoTemCo2()
    {
        Assert.Null(HiveSimulator.Read(Colmeia(model: "starter"), Agora).Co2);
    }

    [Fact]
    public void Simulacao_NuncaMudaOEstadoDosDadosIniciais()
    {
        // A cada hora de 30 dias, cada colmeia dos dados iniciais mantém o estado com que foi pensada.
        using var f = new ApiFactory();
        var hives = f.Query(db => db.Hives.ToList());
        var esperado = new Dictionary<string, string>
        {
            ["Colmeia do Nabão"] = "healthy", ["Sobreiro Velho"] = "danger", ["Alecrim"] = "warning",
            ["Olival de Paialvo"] = "warning", ["Carvalhal"] = "healthy", ["Rosmaninho"] = "healthy",
        };

        foreach (var hive in hives)
            for (var hora = 0; hora < 30 * 24; hora++)
            {
                var at = Agora.AddHours(hora);
                var conditions = HiveRules.Conditions(hive.Model, HiveSimulator.Read(hive, at));
                Assert.Equal(esperado[hive.Name], HiveRules.Status(conditions, at, at));
            }
    }

    // ---- Limiares (contrato: "Estado e alertas") ----

    [Theory]
    [InlineData("pro", 37.0, 1500.0, 80, 80, false, "healthy")]
    [InlineData("pro", 37.1, 1500.0, 80, 80, false, "danger")]
    [InlineData("pro", 35.0, 1500.0, 80, 80, true, "danger")]
    [InlineData("pro", 35.0, 3500.0, 80, 80, false, "healthy")]
    [InlineData("pro", 35.0, 3501.0, 80, 80, false, "warning")]
    [InlineData("starter", 35.0, 9999.0, 80, 80, false, "healthy")] // o starter não tem sensor de CO₂
    [InlineData("pro", 35.0, 1500.0, 20, 80, false, "healthy")]
    [InlineData("pro", 35.0, 1500.0, 19, 80, false, "warning")]
    [InlineData("pro", 35.0, 1500.0, 80, 30, false, "healthy")]
    [InlineData("pro", 35.0, 1500.0, 80, 29, false, "warning")]
    [InlineData("pro", 35.0, 1500.0, 5, 80, true, "danger")] // o perigo prevalece sobre o alerta
    public void Estado_SegueOsLimiaresDoContrato(string model, double temp, double co2, int battery, int signal,
        bool wasp, string esperado)
    {
        var leitura = new HiveSensors(temp, 14, 60, 30, co2, battery, signal, wasp);

        Assert.Equal(esperado, HiveRules.Status(HiveRules.Conditions(model, leitura), Agora, Agora));
    }

    [Fact]
    public void Estado_SemLeiturasHaMaisDeDuasHoras_FicaOffline()
    {
        var saudavel = HiveRules.Conditions("pro", new HiveSensors(35, 14, 60, 30, 1500, 80, 80, false));
        var perigo = HiveRules.Conditions("pro", new HiveSensors(35, 14, 60, 30, 1500, 80, 80, true));

        Assert.Equal("healthy", HiveRules.Status(saudavel, Agora.AddHours(-2), Agora));
        Assert.Equal("offline", HiveRules.Status(saudavel, Agora.AddHours(-2).AddSeconds(-1), Agora));
        Assert.Equal("danger", HiveRules.Status(perigo, Agora.AddHours(-3), Agora)); // ordem da tabela do contrato
    }

    [Fact]
    public void Condicoes_TemOTipoAGravidadeEUmaMensagemEmPortugues()
    {
        var conditions = HiveRules.Conditions("pro", new HiveSensors(38.2, 14, 60, 30, 3820, 15, 25, true));

        Assert.Equal(
            [("wasp", "danger"), ("temperature", "danger"), ("co2", "warning"), ("battery", "warning"), ("signal", "warning")],
            conditions.Select(c => (c.Type, c.Severity)));
        Assert.Equal("Temperatura interior de 38,2 °C, acima de 37 °C.", conditions[1].Message);
        Assert.Equal("CO₂ a 3820 ppm, acima de 3500 ppm.", conditions[2].Message);
        Assert.Equal("Bateria a 15 %, abaixo de 20 %.", conditions[3].Message);
    }
}
