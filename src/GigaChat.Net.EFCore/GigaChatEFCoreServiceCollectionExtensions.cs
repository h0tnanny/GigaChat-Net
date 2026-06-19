using GigaChat.Net.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GigaChat.Net.EFCore;

/// <summary>
/// Extension methods on <see cref="IServiceCollection"/> for registering the EF Core checkpointer.
/// </summary>
public static class GigaChatEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EFCoreGigaChatAgentThreadStore{TContext}"/> as
    /// <see cref="IGigaChatAgentThreadStore"/> and adds <see cref="IDbContextFactory{TContext}"/>.
    /// Works with any EF Core provider (SQLite, SQL Server, PostgreSQL, …).
    /// </summary>
    /// <typeparam name="TContext">
    /// Application DbContext type that derives from <see cref="GigaChatDbContext"/>.
    /// </typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configureDb">Delegate that configures the <see cref="DbContextOptionsBuilder"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddGigaChatEFCoreThreadStore<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb)
        where TContext : GigaChatDbContext
    {
        services.AddDbContextFactory<TContext>(configureDb);
        services.AddSingleton<IGigaChatAgentThreadStore,
            EFCoreGigaChatAgentThreadStore<TContext>>();
        return services;
    }
}
