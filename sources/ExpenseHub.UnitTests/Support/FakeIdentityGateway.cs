using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.Support;

/// <summary>In-memory identity store used by the account and admin service tests.</summary>
internal sealed class FakeIdentityGateway : IIdentityGateway
{
    private readonly Dictionary<string, (string Email, string Secret, List<string> Roles)> _users = [];

    public int ReplaceRolesCalls { get; private set; }

    public void AddUser(string id, string email, string secret, params string[] roles)
    {
        _users[id] = (email, secret, [.. roles]);
    }

    public IReadOnlyList<string> RolesOf(string id)
    {
        return _users[id].Roles;
    }

    public Task<UserResponse> CreateUserAsync(string email, string secret)
    {
        if (_users.Values.Any(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("The e-mail is already registered.");
        }

        string id = Guid.NewGuid().ToString();
        _users[id] = (email, secret, []);

        return Task.FromResult(new UserResponse(id, email, []));
    }

    public Task<AuthenticatedUser?> AuthenticateAsync(string email, string secret)
    {
        foreach (KeyValuePair<string, (string Email, string Secret, List<string> Roles)> pair in _users)
        {
            if (string.Equals(pair.Value.Email, email, StringComparison.OrdinalIgnoreCase) && pair.Value.Secret == secret)
            {
                return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser(pair.Key, pair.Value.Email, pair.Value.Roles.ToArray()));
            }
        }

        return Task.FromResult<AuthenticatedUser?>(null);
    }

    public Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<UserResponse> result = _users
            .Select(pair => new UserResponse(pair.Key, pair.Value.Email, pair.Value.Roles.ToArray()))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<UserResponse> ReplaceRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        ReplaceRolesCalls++;

        if (!_users.TryGetValue(userId, out (string Email, string Secret, List<string> Roles) user))
        {
            throw new NotFoundException("User not found.");
        }

        user.Roles.Clear();
        user.Roles.AddRange(roles);

        return Task.FromResult(new UserResponse(userId, user.Email, roles.ToArray()));
    }
}
