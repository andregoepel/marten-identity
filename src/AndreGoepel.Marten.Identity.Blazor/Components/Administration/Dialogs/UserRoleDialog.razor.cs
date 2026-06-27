using Marten;

namespace AndreGoepel.Marten.Identity.Blazor.Components.Administration.Dialogs;

public partial class UserRoleDialog
{
    protected override async Task OnInitializedAsync()
    {
        _user =
            await UserManager.FindByIdAsync(UserId)
            ?? throw new InvalidOperationException("User not found");
        _roles = await RoleManager
            .Roles.Where(role => !role.Deleted)
            .ToListAsync(CancellationToken.None);

        _values = [.. _user.Roles.Select(roleId => roleId)];

        await base.OnInitializedAsync();
    }
}
