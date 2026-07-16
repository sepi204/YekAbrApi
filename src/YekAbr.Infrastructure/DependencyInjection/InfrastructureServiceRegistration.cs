using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using YekAbr.Domain.Interfaces;
using YekAbr.Infrastructure.Cloud;
using YekAbr.Infrastructure.Cloud.GoogleDrive;
using YekAbr.Infrastructure.Identity;
using YekAbr.Infrastructure.Persistence;
using YekAbr.Infrastructure.Repositories;
using YekAbr.Infrastructure.Security;
using YekAbr.Infrastructure.Services.Auth;
using YekAbr.Infrastructure.Services.Cloud;
using YekAbr.Services.DTOs.Auth;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Auth;
using YekAbr.Services.Interfaces.Cloud;
using YekAbr.Services.Validators.Auth;
using YekAbr.Services.Validators.Cloud;

namespace YekAbr.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<CloudTokenEncryptionOptions>(configuration.GetSection(CloudTokenEncryptionOptions.SectionName));
        services.Configure<GoogleDriveOptions>(configuration.GetSection(GoogleDriveOptions.SectionName));

        services.AddDataProtection();
        services.AddMemoryCache();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IConnectedCloudAccountRepository, ConnectedCloudAccountRepository>();
        services.AddScoped<ICloudTransferJobRepository, CloudTransferJobRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<YekAbr.Services.Interfaces.Auth.IAuthService, AuthService>();

        services.AddSingleton<ICloudTokenEncryptionService, CloudTokenEncryptionService>();
        services.AddSingleton<ICloudOAuthStateStore, MemoryCloudOAuthStateStore>();
        services.AddScoped<ICloudProviderClientFactory, CloudProviderClientFactory>();

        services.AddHttpClient<IGoogleDriveProviderClient, GoogleDriveProviderClient>();
        services.AddScoped<ICloudProviderClient>(sp => sp.GetRequiredService<IGoogleDriveProviderClient>());

        services.AddScoped<IGoogleDriveConnectionService, GoogleDriveConnectionService>();
        services.AddScoped<ICloudAccountService, CloudAccountService>();
        services.AddScoped<ICloudAccountCredentialService, CloudAccountCredentialService>();
        services.AddScoped<ICloudFileService, CloudFileService>();

        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<RefreshTokenRequest>, RefreshTokenRequestValidator>();
        services.AddScoped<IValidator<LogoutRequest>, LogoutRequestValidator>();
        services.AddScoped<IValidator<ListCloudItemsRequest>, ListCloudItemsRequestValidator>();
        services.AddScoped<IValidator<CreateCloudFolderRequest>, CreateCloudFolderRequestValidator>();
        services.AddScoped<IValidator<MoveCloudItemRequest>, MoveCloudItemRequestValidator>();
        services.AddScoped<IValidator<RenameCloudItemRequest>, RenameCloudItemRequestValidator>();

        return services;
    }
}
