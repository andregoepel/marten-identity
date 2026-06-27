using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AndreGoepel.Marten.Identity.Http;

/// <summary>
/// Server-side, single-use store for short-lived login handoff payloads
/// (password, 2FA code, recovery code).
/// <para>
/// <see cref="Protect{T}"/> returns an opaque handle (a GUID); the actual
/// payload is kept server-side, DataProtection-encrypted, and removed the first
/// time it is consumed. This means credentials never appear in the query string,
/// browser history, <c>Referer</c> headers, reverse-proxy logs, or web-server
/// access logs (CWE-598), and a captured handoff URL cannot be replayed — the
/// handle is strictly single-use (CWE-294). Entries also carry a short TTL, so an
/// abandoned handoff disappears quickly.
/// </para>
/// </summary>
public sealed class LoginTokenProtector(IDataProtectionProvider provider)
{
    private const string Purpose = "AndreGoepel.Marten.Identity.LoginHandoff";

    // Two minutes is plenty for the round-trip from the form submit to the
    // cookie-write middleware call; long enough to absorb a slow network, short
    // enough that an abandoned handoff is mostly useless.
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly ITimeLimitedDataProtector _protector = provider
        .CreateProtector(Purpose)
        .ToTimeLimitedDataProtector();

    private readonly ConcurrentDictionary<string, PendingHandoff> _pending = new();

    private sealed record PendingHandoff(string Ciphertext, DateTimeOffset ExpiresAt);

    /// <summary>Stashes <paramref name="payload"/> server-side and returns an opaque,
    /// single-use handle to carry in the handoff URL.</summary>
    public string Protect<T>(T payload)
    {
        PruneExpired();

        // Encrypt at rest too, so the payload is not sitting in process memory in
        // the clear while it waits to be consumed.
        var ciphertext = _protector.Protect(JsonSerializer.Serialize(payload), Lifetime);
        var handle = Guid.NewGuid().ToString("N");
        _pending[handle] = new PendingHandoff(ciphertext, DateTimeOffset.UtcNow.Add(Lifetime));
        return handle;
    }

    /// <summary>Consumes the handoff identified by <paramref name="handle"/>. Returns
    /// <c>false</c> for an unknown, already-consumed, expired, or tampered handle. The
    /// handle is removed atomically, so a replay finds nothing.</summary>
    public bool TryConsume<T>(string? handle, out T payload)
    {
        payload = default!;
        if (string.IsNullOrEmpty(handle))
            return false;

        PruneExpired();

        // Atomic remove ⇒ single use. A second request with the same handle misses.
        if (!_pending.TryRemove(handle, out var entry))
            return false;
        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        try
        {
            var json = _protector.Unprotect(entry.Ciphertext);
            var value = JsonSerializer.Deserialize<T>(json);
            if (value is null)
                return false;
            payload = value;
            return true;
        }
        catch (Exception)
        {
            // CryptographicException for tampered/expired ciphertext, JsonException
            // for malformed payloads — both mean "handoff is unusable".
            return false;
        }
    }

    private void PruneExpired()
    {
        if (_pending.IsEmpty)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _pending)
        {
            if (entry.Value.ExpiresAt <= now)
                _pending.TryRemove(entry.Key, out _);
        }
    }
}
