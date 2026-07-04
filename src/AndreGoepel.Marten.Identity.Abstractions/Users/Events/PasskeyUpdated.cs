using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Users.Events;

// Passkey is nullable so the GDPR masking rule can null the entire credential
// payload (public key, credential id, user-chosen name, attestation) at erasure (#67).
public record PasskeyUpdated(UserId UserId, UserPasskeyInfo? Passkey)
{
    public UserId UpdatedBy { get; init; } = UserId;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
