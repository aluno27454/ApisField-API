using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Data;

/// <summary>Gera e encerra os alertas a partir da leitura atual de cada colmeia.</summary>
/// <remarks>
/// Corre quando a app pede os alertas (<c>GET /api/alerts</c>). Como as leituras são simuladas e
/// calculadas na hora, não há leituras a chegar que justifiquem um processo em segundo plano: verificar
/// no momento do pedido dá o mesmo resultado.
/// </remarks>
public static class AlertMonitor
{
    /// <summary>Atualiza os alertas das colmeias <paramref name="hives"/> com a leitura do instante <paramref name="now"/>.</summary>
    /// <remarks>
    /// Cada condição gera um alerta só uma vez enquanto se mantiver (contrato). Quando a condição passa,
    /// o alerta deixa de estar ativo; se voltar, gera um alerta novo.
    /// </remarks>
    public static async Task RefreshAsync(AppDbContext db, IQueryable<Hive> hives, DateTime now)
    {
        // No SQLite, a transação bloqueia logo as outras escritas: dois pedidos ao mesmo tempo atualizam
        // um de cada vez, e o segundo já vê os alertas criados pelo primeiro.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var list = await hives.ToListAsync();
        var ids = list.Select(h => h.Id).ToList();
        var active = await db.Alerts.Where(a => a.Active && ids.Contains(a.HiveId)).ToListAsync();

        foreach (var hive in list)
        {
            var conditions = HiveRules.Conditions(hive.Model, HiveSimulator.Read(hive, now));
            var current = active.Where(a => a.HiveId == hive.Id).ToList();

            foreach (var ended in current.Where(a => conditions.All(c => c.Type != a.Type)))
                ended.Active = false;

            foreach (var started in conditions.Where(c => current.All(a => a.Type != c.Type)))
                db.Alerts.Add(new Alert
                {
                    HiveId = hive.Id, Type = started.Type, Severity = started.Severity,
                    Message = started.Message, Timestamp = now, Active = true,
                });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
