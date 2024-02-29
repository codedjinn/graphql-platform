using System;
using System.Collections.Generic;
using System.Text;
using GreenDonut;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Caching;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution.Options;
using HotChocolate.Execution.Processing;
using HotChocolate.Fetching;
using HotChocolate.Language;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class RequestExecutorServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="IRequestExecutorResolver"/> and related services
    /// to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddGraphQLCore(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions();

        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.TryAddSingleton<DefaultRequestContextAccessor>();
        services.TryAddSingleton<IRequestContextAccessor>(sp => sp.GetRequiredService<DefaultRequestContextAccessor>());

        services.TryAddSingleton<ObjectPool<StringBuilder>>(sp =>
        {
            var provider = sp.GetRequiredService<ObjectPoolProvider>();
            var policy = new StringBuilderPooledObjectPolicy();
            return provider.Create(policy);
        });

        // core services
        services
            .TryAddRequestExecutorFactoryOptionsMonitor()
            .TryAddTypeConverter()
            .TryAddInputFormatter()
            .TryAddInputParser()
            .TryAddDefaultCaches()
            .TryAddDefaultDocumentHashProvider()
            .TryAddDefaultBatchDispatcher()
            .TryAddDefaultDataLoaderRegistry()
            .TryAddIdSerializer()
            .TryAddDataLoaderParameterExpressionBuilder()
            .AddSingleton<ResolverProvider>();

        // pools
        services
            .TryAddResultPool()
            .TryAddResolverTaskPool()
            .TryAddOperationContextPool()
            .TryAddDeferredWorkStatePool()
            .TryAddOperationCompilerPool();

        // global executor services
        services
            .TryAddVariableCoercion()
            .TryAddRequestExecutorResolver();

        // parser
        services.TryAddSingleton(
            sp =>
            {
                var modifiers = sp.GetService<IEnumerable<Action<RequestParserOptions>>?>();

                if (modifiers is null)
                {
                    return ParserOptions.Default;
                }

                var options = new RequestParserOptions();

                foreach (var configure in modifiers)
                {
                    configure(options);
                }

                return new ParserOptions(
                    noLocations: !options.IncludeLocations,
                    maxAllowedNodes: options.MaxAllowedNodes,
                    maxAllowedTokens: options.MaxAllowedTokens,
                    maxAllowedFields: options.MaxAllowedFields);
            });

        return services;
    }

    /// <summary>
    /// Adds the <see cref="IRequestExecutorResolver"/> and related services to the
    /// <see cref="IServiceCollection"/> and configures a named <see cref="IRequestExecutor"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/>.
    /// </param>
    /// <param name="schemaName">
    /// The logical name of the <see cref="ISchema"/> to configure.
    /// </param>
    /// <returns>
    /// An <see cref="IRequestExecutorBuilder"/> that can be used to configure the executor.
    /// </returns>
    public static IRequestExecutorBuilder AddGraphQL(
        this IServiceCollection services,
        string? schemaName = default)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        schemaName ??= Schema.DefaultName;

        services
            .AddGraphQLCore()
            .AddValidation(schemaName);

        return CreateBuilder(services, schemaName);
    }

    /// <summary>
    /// Adds the <see cref="IRequestExecutorResolver"/> and related services to the
    /// <see cref="IServiceCollection"/> and configures a named <see cref="IRequestExecutor"/>.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="IRequestExecutorBuilder"/>.
    /// </param>
    /// <param name="schemaName">
    /// The logical name of the <see cref="ISchema"/> to configure.
    /// </param>
    /// <returns>
    /// An <see cref="IRequestExecutorBuilder"/> that can be used to configure the executor.
    /// </returns>
    public static IRequestExecutorBuilder AddGraphQL(
        this IRequestExecutorBuilder builder,
        string? schemaName = default)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        schemaName ??= Schema.DefaultName;

        builder.Services.AddValidation(schemaName);

        return CreateBuilder(builder.Services, schemaName);
    }

    private static IRequestExecutorBuilder CreateBuilder(
        IServiceCollection services,
        string schemaName)
    {
        var builder = new DefaultRequestExecutorBuilder(services, schemaName);

        builder.Configure(
            (sp, e) =>
            {
                e.OnRequestExecutorEvictedHooks.Add(
                    // when ever we evict this schema we will clear the caches.
                    new OnRequestExecutorEvictedAction(
                        _ => sp.GetRequiredService<IPreparedOperationCache>().Clear()));
            });

        builder.TryAddNoOpTransactionScopeHandler();

        return builder;
    }

    public static IServiceCollection AddDocumentCache(
        this IServiceCollection services,
        int capacity = 100)
    {
        services.RemoveAll<IDocumentCache>();
        services.AddSingleton<IDocumentCache>(
            _ => new DefaultDocumentCache(capacity));
        return services;
    }

    public static IServiceCollection AddOperationCache(
        this IServiceCollection services,
        int capacity = 100)
    {
        services.RemoveAll<IPreparedOperationCache>();
        services.RemoveAll<IComplexityAnalyzerCache>();

        services.AddSingleton<IPreparedOperationCache>(
            _ => new DefaultPreparedOperationCache(capacity));
        services.AddSingleton<IComplexityAnalyzerCache>(
            _ => new DefaultComplexityAnalyzerCache(capacity));

        return services;
    }

    public static IServiceCollection AddMD5DocumentHashProvider(
        this IServiceCollection services,
        HashFormat format = HashFormat.Base64)
    {
        services.RemoveAll<IDocumentHashProvider>();
        services.AddSingleton<IDocumentHashProvider>(
            new MD5DocumentHashProvider(format));
        return services;
    }

    public static IServiceCollection AddSha1DocumentHashProvider(
        this IServiceCollection services,
        HashFormat format = HashFormat.Base64)
    {
        services.RemoveAll<IDocumentHashProvider>();
        services.AddSingleton<IDocumentHashProvider>(
            new Sha1DocumentHashProvider(format));
        return services;
    }

    public static IServiceCollection AddSha256DocumentHashProvider(
        this IServiceCollection services,
        HashFormat format = HashFormat.Base64)
    {
        services.RemoveAll<IDocumentHashProvider>();
        services.AddSingleton<IDocumentHashProvider>(
            new Sha256DocumentHashProvider(format));
        return services;
    }

    public static IServiceCollection AddBatchDispatcher<T>(this IServiceCollection services)
        where T : class, IBatchDispatcher
    {
        services.RemoveAll<IBatchDispatcher>();
        services.AddScoped<IBatchDispatcher, T>();
        return services;
    }

    public static IServiceCollection AddBatchScheduler<T>(this IServiceCollection services)
        where T : class, IBatchScheduler
    {
        services.RemoveAll<IBatchScheduler>();
        services.AddScoped<IBatchScheduler, T>();
        return services;
    }

    public static IServiceCollection AddDefaultBatchDispatcher(this IServiceCollection services)
    {
        services.RemoveAll<IBatchScheduler>();
        services.TryAddDefaultBatchDispatcher();
        return services;
    }
}
