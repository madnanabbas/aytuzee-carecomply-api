using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Domain.AccessManagement;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.AccessManagement;

internal sealed class EfRoleManagement(
    CareComplyDbContext context,
    RoleManager<ApplicationRole> roleManager,
    RoleManagementReader reader,
    RoleCompanyAccessValidator companyAccessValidator,
    RolePermissionSynchronizer permissionSynchronizer) : IRoleManagement
{
    public Task<IReadOnlyList<RoleManagementListItem>> ListAsync(
        CancellationToken cancellationToken) =>
        reader.ListAsync(cancellationToken);

    public Task<RoleManagementDetails?> GetAsync(
        Guid roleId,
        CancellationToken cancellationToken) =>
        reader.GetAsync(roleId, cancellationToken);

    public Task<IReadOnlyList<PermissionCatalogModule>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken) =>
        reader.GetPermissionCatalogAsync(cancellationToken);

    public async Task<RoleMutationResult> CreateAsync(
        Guid? companyId,
        string name,
        string? description,
        IReadOnlyList<int> permissionIds,
        CancellationToken cancellationToken)
    {
        var permissionValidation = await permissionSynchronizer.ValidateAsync(
            permissionIds,
            cancellationToken);
        if (!permissionValidation.IsValid)
        {
            return Failed(RoleMutationStatus.PermissionNotFound);
        }

        var targetCompanyId = await companyAccessValidator.ResolveCreateCompanyAsync(
            companyId,
            cancellationToken);
        if (targetCompanyId is null)
        {
            return Failed(RoleMutationStatus.Unavailable);
        }

        var applicationRole = new ApplicationRole
        {
            Name = name.Trim(),
            CompanyId = targetCompanyId.Value,
        };
        var definition = RoleDefinition.Create(
            applicationRole.Id,
            name,
            description,
            permissionValidation.PermissionIds);

        var createResult = await roleManager.CreateAsync(applicationRole);
        if (!createResult.Succeeded)
        {
            return Failed(RoleManagementFailureClassifier.ClassifyCreate(createResult));
        }

        context.Add(new RoleProfile(applicationRole.Id, definition.Description));
        await context.AddRangeAsync(
            definition.PermissionIds.Select(
                permissionId => new RolePermission(applicationRole.Id, permissionId)),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new(
            RoleMutationStatus.Succeeded,
            await reader.GetAsync(applicationRole.Id, cancellationToken));
    }

    public async Task<RoleMutationResult> UpdateGeneralAsync(
        Guid roleId,
        string name,
        string? description,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null
            || !await companyAccessValidator.CanAccessAsync(role, cancellationToken))
        {
            return Failed(RoleMutationStatus.NotFound);
        }

        if (!IdentityEditorVersion.Matches(role.ConcurrencyStamp ?? string.Empty, editorVersion))
        {
            return Failed(RoleMutationStatus.Conflict);
        }

        var profile = await context.Set<RoleProfile>()
            .SingleOrDefaultAsync(value => value.RoleId == roleId, cancellationToken);
        var permissionIds = await context.Set<RolePermission>()
            .AsNoTracking()
            .Where(value => value.RoleId == roleId)
            .Select(value => value.PermissionId)
            .ToArrayAsync(cancellationToken);
        var definition = RoleDefinition.Restore(
            role.Id,
            role.Name ?? string.Empty,
            profile?.Description,
            permissionIds);
        if (!definition.ChangeGeneral(name, description))
        {
            return new(
                RoleMutationStatus.NoChanges,
                await reader.GetAsync(roleId, cancellationToken));
        }

        role.Name = definition.Name;
        var identityResult = await roleManager.UpdateAsync(role);
        if (!identityResult.Succeeded)
        {
            return Failed(RoleManagementFailureClassifier.ClassifyUpdate(identityResult));
        }

        if (profile is null)
        {
            context.Add(new RoleProfile(roleId, definition.Description));
        }
        else
        {
            profile.ChangeDescription(definition.Description);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new(
            RoleMutationStatus.Succeeded,
            await reader.GetAsync(roleId, cancellationToken));
    }

    public async Task<RolePermissionMutationResult> ReplacePermissionsAsync(
        Guid roleId,
        IReadOnlyList<int> permissionIds,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null
            || !await companyAccessValidator.CanAccessAsync(role, cancellationToken))
        {
            return PermissionResult(RoleMutationStatus.NotFound);
        }

        if (!IdentityEditorVersion.Matches(role.ConcurrencyStamp ?? string.Empty, editorVersion))
        {
            return PermissionResult(RoleMutationStatus.Conflict);
        }

        var validation = await permissionSynchronizer.ValidateAsync(
            permissionIds,
            cancellationToken);
        if (!validation.IsValid)
        {
            return PermissionResult(RoleMutationStatus.PermissionNotFound);
        }

        var change = await permissionSynchronizer.BuildChangePlanAsync(
            role,
            validation.PermissionIds,
            cancellationToken);
        if (!change.HasChanges)
        {
            return new(
                RoleMutationStatus.NoChanges,
                await reader.GetAsync(roleId, cancellationToken),
                [],
                []);
        }

        if (await permissionSynchronizer.WouldRemoveLastRoleManagerAsync(
                roleId,
                change.RequestedPermissionIds,
                cancellationToken))
        {
            return PermissionResult(RoleMutationStatus.LastRoleManager);
        }

        await permissionSynchronizer.StageAsync(change, roleId, cancellationToken);

        var roleUpdate = await roleManager.UpdateAsync(role);
        if (!roleUpdate.Succeeded)
        {
            return PermissionResult(RoleMutationStatus.Conflict);
        }

        await context.SaveChangesAsync(cancellationToken);
        if (!await permissionSynchronizer.RevokeAssignedSessionsAsync(roleId, cancellationToken))
        {
            return PermissionResult(RoleMutationStatus.Unavailable);
        }

        return new(
            RoleMutationStatus.Succeeded,
            await reader.GetAsync(roleId, cancellationToken),
            change.AddedPermissionIds,
            change.RemovedPermissionIds);
    }

    private static RoleMutationResult Failed(RoleMutationStatus status) =>
        new(status, null);

    private static RolePermissionMutationResult PermissionResult(RoleMutationStatus status) =>
        new(status, null, [], []);
}
