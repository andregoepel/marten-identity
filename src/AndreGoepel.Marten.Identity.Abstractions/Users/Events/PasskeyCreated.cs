using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Users.Events;

// Passkey is nullable so the GDPR masking rule can null the entire credential
// payload (public key, credential id, user-chosen name, attestation) at erasure (#67).
public record PasskeyCreated(UserId UserId, UserPasskeyInfo? Passkey)
{
    public UserId CreatedBy { get; init; } = UserId;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
