using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Core.AppSettings;
using Shop.Core.Interfaces;
using Shop.Domain.Entities.Customer;
using Shop.Infrastructure.Data.Extensions;

namespace Shop.Infrastructure.Data.Context;

public sealed class ShopContext : DbContext
{
    #region Constructor

    private readonly string _collation;
    private readonly ICurrentUserProvider _currentUserProvider;

    public ShopContext(DbContextOptions<ShopContext> dbOptions) : base(dbOptions)
    {
        ChangeTracker.LazyLoadingEnabled = false;
    }

    public ShopContext(
        DbContextOptions<ShopContext> dbOptions,
        ICurrentUserProvider currentUserProvider) : this(dbOptions)
    {
        _currentUserProvider = currentUserProvider;
    }

    public ShopContext(
        DbContextOptions<ShopContext> dbOptions,
        ICurrentUserProvider currentUserProvider,
        IOptions<ConnectionOptions> connectionOptions) : this(dbOptions, currentUserProvider)
    {
        _collation = connectionOptions.Value.Collation;
    }

    #endregion

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (!string.IsNullOrWhiteSpace(_collation))
            modelBuilder.UseCollation(_collation);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.RemoveCascadeDeleteConvention();
    }

    #region SaveChanges

    public override int SaveChanges()
    {
        OnBeforeSaving();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        OnBeforeSaving();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Aplica a auditoria antes de salvar.
    /// </summary>
    private void OnBeforeSaving()
    {
        var dtNow = DateTime.Now;
        var userId = _currentUserProvider?.GetCurrentUserId();

        foreach (var entry in ChangeTracker.Entries<IAudit>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetAdded(userId, dtNow);
                    break;

                case EntityState.Modified:
                    entry.Entity.SetModified(userId, dtNow);
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.SetDeleted(userId, dtNow);
                    break;
            }
        }
    }

    #endregion
}