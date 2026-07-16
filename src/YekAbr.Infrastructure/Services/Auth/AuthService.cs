using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using YekAbr.Domain.Constants;
using YekAbr.Domain.Entities;
using YekAbr.Domain.Interfaces;
using YekAbr.Infrastructure.Identity;
using YekAbr.Infrastructure.Security;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Auth;
using YekAbr.Services.Interfaces.Auth;
using YekAbr.Services.Services.Auth;

namespace YekAbr.Infrastructure.Services.Auth;

public sealed class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "Invalid username/email or password.";
    private const string InvalidRefreshTokenMessage = "Invalid or expired refresh token.";

    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;

    public AuthService(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<AppUser> signInManager,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService jwtTokenService,
        Microsoft.Extensions.Options.IOptions<JwtOptions> jwtOptions,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        IValidator<LogoutRequest> logoutValidator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
    }

    public async Task<Result<AuthTokensDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidationHelper.ValidateAsync(_registerValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<AuthTokensDto>.Failed("Validation failed.", validationErrors);
        }

        if (await _userManager.Users.AnyAsync(x => x.NormalizedUserName == request.Username.ToUpper(), cancellationToken))
        {
            return Result<AuthTokensDto>.Failed("Username is already taken.", new Dictionary<string, string[]>
            {
                [nameof(request.Username)] = ["Username is already taken."]
            });
        }

        if (await _userManager.Users.AnyAsync(x => x.NormalizedEmail == request.Email.ToUpper(), cancellationToken))
        {
            return Result<AuthTokensDto>.Failed("Email is already in use.", new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = ["Email is already in use."]
            });
        }

        var user = new AppUser
        {
            UserName = request.Username.Trim(),
            Email = request.Email.Trim(),
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result<AuthTokensDto>.Failed("Registration failed.", MapIdentityErrors(createResult.Errors));
        }

        await EnsureRoleExistsAsync(RoleNames.User);
        await _userManager.AddToRoleAsync(user, RoleNames.User);

        var authTokens = await IssueTokensAsync(user, cancellationToken);
        return Result<AuthTokensDto>.Succeeded(authTokens, "User registered successfully.");
    }

    public async Task<Result<AuthTokensDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidationHelper.ValidateAsync(_loginValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<AuthTokensDto>.Failed("Validation failed.", validationErrors);
        }

        var normalizedInput = request.UsernameOrEmail.Trim().ToUpper();
        var user = await _userManager.Users.FirstOrDefaultAsync(
            x => x.NormalizedUserName == normalizedInput || x.NormalizedEmail == normalizedInput,
            cancellationToken);

        if (user is null)
        {
            return Result<AuthTokensDto>.Failed(InvalidCredentialsMessage);
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!signInResult.Succeeded)
        {
            return Result<AuthTokensDto>.Failed(InvalidCredentialsMessage);
        }

        var authTokens = await IssueTokensAsync(user, cancellationToken);
        return Result<AuthTokensDto>.Succeeded(authTokens, "Login successful.");
    }

    public async Task<Result<AuthTokensDto>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidationHelper.ValidateAsync(_refreshValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<AuthTokensDto>.Failed("Validation failed.", validationErrors);
        }

        var existingRefreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);
        if (existingRefreshToken is null || !existingRefreshToken.IsActive)
        {
            return Result<AuthTokensDto>.Failed(InvalidRefreshTokenMessage);
        }

        var user = await _userManager.FindByIdAsync(existingRefreshToken.UserId);
        if (user is null)
        {
            return Result<AuthTokensDto>.Failed(InvalidRefreshTokenMessage);
        }

        var newRefreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        existingRefreshToken.RevokedAtUtc = DateTime.UtcNow;
        existingRefreshToken.ReplacedByToken = newRefreshTokenValue;
        existingRefreshToken.RevokedReason = "Rotated";

        var authTokens = await BuildTokenResponseAsync(user, newRefreshTokenValue, cancellationToken);
        await _refreshTokenRepository.AddAsync(CreateRefreshToken(user.Id, newRefreshTokenValue), cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result<AuthTokensDto>.Succeeded(authTokens, "Token refreshed successfully.");
    }

    public async Task<Result<object>> LogoutAsync(string userId, LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidationHelper.ValidateAsync(_logoutValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<object>.Failed("Validation failed.", validationErrors);
        }

        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);
        if (refreshToken is null || refreshToken.UserId != userId)
        {
            return Result<object>.Failed("Refresh token was not found.");
        }

        if (!refreshToken.IsRevoked)
        {
            refreshToken.RevokedAtUtc = DateTime.UtcNow;
            refreshToken.RevokedReason = "Logged out";
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }

        return Result<object>.Succeeded(new { }, "Logout successful.");
    }

    public async Task<Result<object>> LogoutAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var refreshTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(userId, cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAtUtc = DateTime.UtcNow;
            refreshToken.RevokedReason = "Logged out from all devices";
        }

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        return Result<object>.Succeeded(new { }, "All active refresh tokens were revoked.");
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result<UserDto>.Failed("User was not found.");
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        return Result<UserDto>.Succeeded(MapUser(user, roles), "Current user retrieved successfully.");
    }

    private async Task<AuthTokensDto> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var refreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        var authTokens = await BuildTokenResponseAsync(user, refreshTokenValue, cancellationToken);

        await _refreshTokenRepository.AddAsync(CreateRefreshToken(user.Id, refreshTokenValue), cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return authTokens;
    }

    private async Task<AuthTokensDto> BuildTokenResponseAsync(AppUser user, string refreshTokenValue, CancellationToken cancellationToken)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        var accessTokenResult = await _jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.UserName!,
            user.Email!,
            roles);
        var accessToken = accessTokenResult.Token;
        var accessTokenExpiresAtUtc = accessTokenResult.ExpiresAtUtc;

        return new AuthTokensDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = accessTokenExpiresAtUtc,
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            User = MapUser(user, roles)
        };
    }

    private RefreshToken CreateRefreshToken(string userId, string token)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        };
    }

    private static UserDto MapUser(AppUser user, IReadOnlyCollection<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email!,
            Roles = roles
        };
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private static IReadOnlyDictionary<string, string[]> MapIdentityErrors(IEnumerable<IdentityError> errors)
    {
        return errors
            .GroupBy(x => x.Code)
            .ToDictionary(
                x => x.Key,
                x => x.Select(e => e.Description).Distinct().ToArray() as string[]);
    }
}
