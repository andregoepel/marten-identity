using Marten;

namespace AndreGoepel.Marten.Identity.Blazor.Components.Administration.Pages;

public partial class Roles
{
    private async Task LoadRolesAsync()
    {
        var query = ShowDeleted ? RoleManager.Roles : RoleManager.Roles.Where(r => !r.Deleted);

        _roles = await query.ToListAsync(CancellationToken.None);
    }
}
