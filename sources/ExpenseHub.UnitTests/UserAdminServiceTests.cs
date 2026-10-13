using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Tests for user admin service behavior.</summary>
[TestClass]
public sealed class UserAdminServiceTests
{
    private const string ValidCredential = "Abcd1234";

    private readonly FakeIdentityGateway _identity = new();
    private readonly UserAdminService _admin;

    /// <summary>Initializes a new instance of the <see cref="UserAdminServiceTests"/> class.</summary>
    public UserAdminServiceTests()
    {
        _admin = new UserAdminService(_identity);
        _identity.AddUser("admin-1", "admin@example.com", ValidCredential, AppRoles.Admin);
        _identity.AddUser("user-1", "ana@example.com", ValidCredential);
    }

    /// <summary>List users: returns every user to an admin.</summary>
    [TestMethod]
    public async Task ListUsers_ReturnsEveryUserToAnAdmin()
    {
        IReadOnlyList<UserResponse> users = await _admin.ListUsersAsync(TestData.Admin("admin-1"), CancellationToken.None);

        Assert.HasCount(2, users);
    }

    /// <summary>List users: is forbidden for every other role.</summary>
    [TestMethod]
    public async Task ListUsers_IsForbiddenForEveryOtherRole()
    {
        foreach (UserContext user in new[] { TestData.Employee(), TestData.Approver(), TestData.Finance(), TestData.Auditor(), TestData.User("nobody-1") })
        {
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _admin.ListUsersAsync(user, CancellationToken.None));
        }
    }

    /// <summary>Update roles: assigns known roles.</summary>
    [TestMethod]
    public async Task UpdateRoles_AssignsKnownRoles()
    {
        UpdateRolesRequest request = new() { Roles = [AppRoles.Employee, AppRoles.Approver] };

        UserResponse updated = await _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "user-1", request, CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { AppRoles.Employee, AppRoles.Approver }, updated.Roles.ToArray());
        CollectionAssert.AreEquivalent(new[] { AppRoles.Employee, AppRoles.Approver }, _identity.RolesOf("user-1").ToArray());
    }

    /// <summary>Update roles: removes roles that are no longer listed.</summary>
    [TestMethod]
    public async Task UpdateRoles_RemovesRolesThatAreNoLongerListed()
    {
        _identity.AddUser("user-2", "bia@example.com", ValidCredential, AppRoles.Employee, AppRoles.Finance);

        await _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "user-2", new UpdateRolesRequest { Roles = [AppRoles.Employee] }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { AppRoles.Employee }, _identity.RolesOf("user-2").ToArray());
    }

    /// <summary>Update roles: normalizes case and ignores duplicates.</summary>
    [TestMethod]
    public async Task UpdateRoles_NormalizesCaseAndIgnoresDuplicates()
    {
        UpdateRolesRequest request = new() { Roles = ["employee", "EMPLOYEE", " Auditor "] };

        UserResponse updated = await _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "user-1", request, CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { AppRoles.Employee, AppRoles.Auditor }, updated.Roles.ToArray());
    }

    /// <summary>Update roles: allows clearing all roles.</summary>
    [TestMethod]
    public async Task UpdateRoles_AllowsClearingAllRoles()
    {
        _identity.AddUser("user-2", "bia@example.com", ValidCredential, AppRoles.Employee);

        UserResponse updated = await _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "user-2", new UpdateRolesRequest { Roles = [] }, CancellationToken.None);

        Assert.IsEmpty(updated.Roles);
    }

    /// <summary>Update roles: rejects unknown roles without changing anything.</summary>
    [TestMethod]
    public async Task UpdateRoles_RejectsUnknownRolesWithoutChangingAnything()
    {
        UpdateRolesRequest request = new() { Roles = [AppRoles.Employee, "SuperUser"] };

        await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "user-1", request, CancellationToken.None));

        Assert.AreEqual(0, _identity.ReplaceRolesCalls);
        Assert.IsEmpty(_identity.RolesOf("user-1"));
    }

    /// <summary>Update roles: requires the roles field.</summary>
    [TestMethod]
    public async Task UpdateRoles_RequiresTheRolesField()
    {
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "user-1", new UpdateRolesRequest { Roles = null }, CancellationToken.None));
    }

    /// <summary>Update roles: does not let an admin remove their own admin role.</summary>
    [TestMethod]
    public async Task UpdateRoles_DoesNotLetAnAdminRemoveTheirOwnAdminRole()
    {
        UpdateRolesRequest request = new() { Roles = [AppRoles.Employee] };

        await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "admin-1", request, CancellationToken.None));

        CollectionAssert.AreEqual(new[] { AppRoles.Admin }, _identity.RolesOf("admin-1").ToArray());
    }

    /// <summary>Update roles: lets an admin keep their own admin role while changing others.</summary>
    [TestMethod]
    public async Task UpdateRoles_LetsAnAdminKeepTheirOwnAdminRoleWhileChangingOthers()
    {
        UpdateRolesRequest request = new() { Roles = [AppRoles.Admin, AppRoles.Employee] };

        UserResponse updated = await _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "admin-1", request, CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { AppRoles.Admin, AppRoles.Employee }, updated.Roles.ToArray());
    }

    /// <summary>Update roles: allows removing the admin role from another admin.</summary>
    [TestMethod]
    public async Task UpdateRoles_AllowsRemovingTheAdminRoleFromAnotherAdmin()
    {
        _identity.AddUser("admin-2", "admin2@example.com", ValidCredential, AppRoles.Admin);

        UserResponse updated = await _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "admin-2", new UpdateRolesRequest { Roles = [] }, CancellationToken.None);

        Assert.IsEmpty(updated.Roles);
    }

    /// <summary>Update roles: is not found for unknown users.</summary>
    [TestMethod]
    public async Task UpdateRoles_IsNotFoundForUnknownUsers()
    {
        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _admin.UpdateRolesAsync(TestData.Admin("admin-1"), "ghost", new UpdateRolesRequest { Roles = [AppRoles.Employee] }, CancellationToken.None));
    }

    /// <summary>Update roles: is forbidden for every other role.</summary>
    [TestMethod]
    public async Task UpdateRoles_IsForbiddenForEveryOtherRole()
    {
        UpdateRolesRequest request = new() { Roles = [AppRoles.Admin] };

        foreach (UserContext user in new[] { TestData.Employee(), TestData.Approver(), TestData.Finance(), TestData.Auditor(), TestData.User("nobody-1") })
        {
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _admin.UpdateRolesAsync(user, "user-1", request, CancellationToken.None));
        }

        Assert.AreEqual(0, _identity.ReplaceRolesCalls);
    }
}
