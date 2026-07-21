using AndreGoepel.Marten.Identity.Blazor.Email;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Users;
using Marten;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Blazor.Components.Administration.Dialogs;

public partial class InviteUser
{
    [Inject]
    private RoleManager<Role> RoleManager { get; set; } = default!;

    [Inject]
    private UserInvitationMailer InvitationMailer { get; set; } = default!;

    private string _email = "";
    private IReadOnlyList<Role> _roles = [];
    private IEnumerable<string> _selectedRoles = [];
    private bool _busy;

    protected override async Task OnInitializedAsync()
    {
        _roles = await RoleManager
            .Roles.Where(role => !role.Deleted)
            .ToListAsync(CancellationToken.None);
    }

    private async Task SendInvitationAsync()
    {
        if (string.IsNullOrWhiteSpace(_email))
        {
            await DialogService.Alert(T("InviteUser.EmailRequired"), T("InviteUser.DialogTitle"));
            return;
        }

        _busy = true;
        try
        {
            var result = await InvitationMailer.InviteAsync(
                _email.Trim(),
                _selectedRoles.ToArray()
            );
            if (!result.Succeeded)
            {
                await DialogService.Alert(
                    string.Join(", ", result.Errors.Select(e => e.Description)),
                    T("InviteUser.InviteFailedTitle")
                );
                return;
            }

            DialogService.Close(true);
        }
        finally
        {
            _busy = false;
        }
    }
}
