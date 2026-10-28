using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Tickets;
using TicketFlow.Application.Attachments;
using TicketFlow.Infrastructure.Attachments;
using TicketFlow.Infrastructure.Identity;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Application.Search;
using TicketFlow.Infrastructure.Search;

namespace TicketFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<TicketFlowDbContext>((provider, options) =>
        {
            var connection = provider.GetRequiredService<IConfiguration>().GetConnectionString("Database");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Configure ConnectionStrings:Database before using TicketFlow persistence.");
            options.UseNpgsql(connection);
        });
        services.AddAuthentication();
        services.AddHttpContextAccessor();
        services.AddDataProtection().SetApplicationName("TicketFlow");
        services.AddIdentityCore<AppUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<TicketFlowDbContext>()
            .AddSignInManager().AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
        services.AddSingleton(provider => AuthSettings.Load(provider.GetRequiredService<IConfiguration>(), provider.GetRequiredService<IHostEnvironment>()));
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPasswordResetSender, SmtpPasswordResetSender>();
        services.AddScoped<ITicketStore, TicketStore>();
        services.AddScoped<TicketWorkflow>();
        services.AddSingleton(provider => SearchSettings.Load(provider.GetRequiredService<IConfiguration>()));
        services.AddHttpClient<ITicketSearchIndex, ElasticsearchTicketIndex>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<ISearchTicketStore, SearchTicketStore>();
        services.AddScoped<TicketSearchWorkflow>();
        services.AddScoped<SearchProjectionWorkflow>();
        services.AddScoped<IAttachmentStorage, S3AttachmentStorage>();
        services.AddScoped<AttachmentWorkflow>();
        services.AddScoped<IDevelopmentSeeder, DevelopmentSeeder>();
        return services;
    }
}
