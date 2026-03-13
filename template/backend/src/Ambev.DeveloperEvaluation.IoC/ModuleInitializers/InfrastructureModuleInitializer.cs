using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.MongoDB;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Ambev.DeveloperEvaluation.IoC.ModuleInitializers;

public class InfrastructureModuleInitializer : IModuleInitializer
{
    public void Initialize(WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        // PostgreSQL — transactional write store
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<DefaultContext>());
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ISaleRepository, SaleRepository>();

        // MongoDB — denormalized read store
        var mongoConnectionString = configuration.GetConnectionString("MongoConnection")
            ?? throw new InvalidOperationException("MongoDB connection string 'MongoConnection' is not configured.");

        builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
        builder.Services.AddScoped<MongoDbContext>();

        // Redis — distributed cache
        var redisConnectionString = configuration.GetConnectionString("RedisConnection")
            ?? throw new InvalidOperationException("Redis connection string 'RedisConnection' is not configured.");

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "DeveloperEvaluation:";
        });
    }
}
