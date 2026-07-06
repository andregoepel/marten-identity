namespace AndreGoepel.Marten.Identity.Blazor.Features;

/// <summary>
/// Source of truth for identity feature availability (#66). The library ships an
/// options-backed default (<see cref="OptionsIdentityFeatureProvider"/>); a host that
/// persists the flags (e.g. an admin-editable store) registers its own implementation
/// whose value takes precedence over configuration. The method is asynchronous because a
/// host implementation typically reads a database.
/// </summary>
public interface IIdentityFeatureProvider
{
    ValueTask<IdentityFeatureFlags> GetAsync(CancellationToken cancellationToken = default);
}
