using System.Threading.Tasks;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application;

/// <summary>Registration and credential validation.</summary>
internal sealed class AccountService
{
    private readonly IIdentityGateway _identity;

    public AccountService(IIdentityGateway identity)
    {
        _identity = identity;
    }

    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        RequestValidator.Validate(request);

        return await _identity.CreateUserAsync(request.Email!.Trim(), request.Password!);
    }

    public async Task<AuthenticatedUser> LoginAsync(LoginRequest request)
    {
        RequestValidator.Validate(request);

        AuthenticatedUser? user = await _identity.AuthenticateAsync(request.Email!.Trim(), request.Password!);

        return user ?? throw new UnauthorizedException("Invalid e-mail or password.");
    }
}
