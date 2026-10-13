using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Tests for account service behavior.</summary>
[TestClass]
public sealed class AccountServiceTests
{
    private const string ValidCredential = "Abcd1234";
    private const string WrongCredential = "Wrong1234";
    private const string ShortCredential = "short";

    private readonly FakeIdentityGateway _identity = new();
    private readonly AccountService _accounts;

    /// <summary>Initializes a new instance of the <see cref="AccountServiceTests"/> class.</summary>
    public AccountServiceTests()
    {
        _accounts = new AccountService(_identity);
    }

    /// <summary>Register: creates a user without roles.</summary>
    [TestMethod]
    public async Task Register_CreatesAUserWithoutRoles()
    {
        UserResponse user = await _accounts.RegisterAsync(new RegisterRequest { Email = "ana@example.com", Password = ValidCredential });

        Assert.AreEqual("ana@example.com", user.Email);
        Assert.IsEmpty(user.Roles);
        Assert.IsEmpty(_identity.RolesOf(user.Id));
    }

    /// <summary>Register: trims the email.</summary>
    [TestMethod]
    public async Task Register_TrimsTheEmail()
    {
        UserResponse user = await _accounts.RegisterAsync(new RegisterRequest { Email = "  ana@example.com ", Password = ValidCredential });

        Assert.AreEqual("ana@example.com", user.Email);
    }

    /// <summary>Register: rejects invalid input.</summary>
    [TestMethod]
    public async Task Register_RejectsInvalidInput()
    {
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _accounts.RegisterAsync(new RegisterRequest { Email = null, Password = ValidCredential }));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _accounts.RegisterAsync(new RegisterRequest { Email = "not-an-email", Password = ValidCredential }));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _accounts.RegisterAsync(new RegisterRequest { Email = "ana@example.com", Password = null }));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _accounts.RegisterAsync(new RegisterRequest { Email = "ana@example.com", Password = ShortCredential }));
    }

    /// <summary>Register: rejects a duplicated email with conflict.</summary>
    [TestMethod]
    public async Task Register_RejectsADuplicatedEmailWithConflict()
    {
        await _accounts.RegisterAsync(new RegisterRequest { Email = "ana@example.com", Password = ValidCredential });

        await Assert.ThrowsExactlyAsync<ConflictException>(
            () => _accounts.RegisterAsync(new RegisterRequest { Email = "ANA@example.com", Password = ValidCredential }));
    }

    /// <summary>Login: returns the user with its roles.</summary>
    [TestMethod]
    public async Task Login_ReturnsTheUserWithItsRoles()
    {
        _identity.AddUser("user-1", "ana@example.com", ValidCredential, AppRoles.Employee, AppRoles.Approver);

        AuthenticatedUser user = await _accounts.LoginAsync(new LoginRequest { Email = "ana@example.com", Password = ValidCredential });

        Assert.AreEqual("user-1", user.Id);
        Assert.AreEqual("ana@example.com", user.Email);
        CollectionAssert.AreEquivalent(new[] { AppRoles.Employee, AppRoles.Approver }, user.Roles.ToArray());
    }

    /// <summary>Login: with a wrong secret is unauthorized.</summary>
    [TestMethod]
    public async Task Login_WithAWrongSecretIsUnauthorized()
    {
        _identity.AddUser("user-1", "ana@example.com", ValidCredential, AppRoles.Employee);

        await Assert.ThrowsExactlyAsync<UnauthorizedException>(
            () => _accounts.LoginAsync(new LoginRequest { Email = "ana@example.com", Password = WrongCredential }));
    }

    /// <summary>Login: with an unknown user is unauthorized.</summary>
    [TestMethod]
    public async Task Login_WithAnUnknownUserIsUnauthorized()
    {
        await Assert.ThrowsExactlyAsync<UnauthorizedException>(
            () => _accounts.LoginAsync(new LoginRequest { Email = "ghost@example.com", Password = ValidCredential }));
    }

    /// <summary>Login: with missing fields is a bad request.</summary>
    [TestMethod]
    public async Task Login_WithMissingFieldsIsABadRequest()
    {
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _accounts.LoginAsync(new LoginRequest { Email = null, Password = null }));
    }
}
