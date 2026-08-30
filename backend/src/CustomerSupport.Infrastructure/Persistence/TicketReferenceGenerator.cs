using System.Data;
using CustomerSupport.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CustomerSupport.Infrastructure.Persistence;

/// <summary>
/// Issues <c>TKT-nnnnnn</c> from a SQL Server sequence.
///
/// <c>NEXT VALUE FOR</c> is atomic and does not participate in the caller's transaction, so a
/// rolled-back ticket creation burns a number rather than handing the same one to the next caller.
/// Burning references is the correct trade: gaps in a reference series are unremarkable, two
/// customers quoting <c>TKT-001042</c> is not.
/// </summary>
public class TicketReferenceGenerator(AppDbContext db) : ITicketReferenceGenerator
{
    /// <summary>
    /// Run as a raw command rather than through <c>Database.SqlQuery&lt;T&gt;</c>.
    ///
    /// That was the first attempt and SQL Server rejects it: <c>SqlQuery</c> composes the text into
    /// a derived table (<c>SELECT ... FROM (&lt;sql&gt;) AS x</c>), and <c>NEXT VALUE FOR</c> is
    /// explicitly not allowed in a sub-query — error 11719. The statement has to reach the server
    /// exactly as written, which means a command, not a composable query.
    /// </summary>
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();

        // The context may or may not already own an open connection depending on whether the caller
        // is inside a transaction. Close only what this method opened.
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT NEXT VALUE FOR [dbo].[{AppDbContext.TicketReferenceSequenceName}]";

            if (db.Database.CurrentTransaction is not null)
            {
                command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
            }

            var next = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            return $"TKT-{next:D6}";
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
