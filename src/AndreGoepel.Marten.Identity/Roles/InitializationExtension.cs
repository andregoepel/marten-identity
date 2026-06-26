using JasperFx.Events.Projections;
using Marten;

namespace AndreGoepel.Marten.Identity.Roles;

internal static class InitializationExtension
{
    public static void InitializeRolesStore(this StoreOptions options)
    {
        options
            .Schema.For<Role>()
            .Identity(x => x.StreamId)
            .Duplicate(
                x => x.NormalizedName,
                configure: index =>
                {
                    // Enforce role-name uniqueness at the database level so two roles
                    // with the same name can never coexist (e.g. a frontend creating a
                    // second "Administrator"). Partial: soft-deleted roles keep their
                    // name (Deleted = true), so the predicate excludes them — otherwise a
                    // name could never be reused after a role is deleted.
                    index.IsUnique = true;
                    index.Predicate = "(data ->> 'Deleted')::boolean = false";
                }
            );

        options.Projections.Add<RoleProjection>(ProjectionLifecycle.Inline);
    }
}
