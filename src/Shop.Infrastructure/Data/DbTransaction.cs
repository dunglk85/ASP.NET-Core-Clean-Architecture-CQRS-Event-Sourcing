using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shop.Core.Interfaces;

namespace Shop.Infrastructure.Data;

public class DbTransaction<TContext> : IDbTransaction<TContext> where TContext : DbContext
{
    private readonly TContext _context;
    private readonly ILogger<DbTransaction<TContext>> _logger;

    public DbTransaction(TContext context, ILogger<DbTransaction<TContext>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            _logger.LogInformation("----- Begin transaction: '{TransactionId}'", transaction.TransactionId);

            await operation();

            try
            {
                var rowsAffected = await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("----- Commit transaction: '{TransactionId}'", transaction.TransactionId);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "----- Transaction successfully confirmed: '{TransactionId}', Rows Affected: {RowsAffected}",
                    rowsAffected, transaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unexpected exception occurred while committing the transaction: '{TransactionId}', message: {Message}",
                    transaction.TransactionId, ex.Message);

                await transaction.RollbackAsync(cancellationToken);

                throw;
            }
        });
    }
}