using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MiniSteam.Desktop.Services;

public sealed class SecureSessionStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MiniSteam.Desktop.Session.v1");
    private readonly string _sessionPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SecureSessionStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiniSteam");

        _sessionPath = Path.Combine(directory, "session.dat");
    }

    public SecureSessionRecord? TryLoad()
    {
        if (!File.Exists(_sessionPath))
        {
            return null;
        }

        try
        {
            var encrypted = File.ReadAllBytes(_sessionPath);
            var clearBytes = ProtectedData.Unprotect(
                encrypted,
                Entropy,
                DataProtectionScope.CurrentUser);

            var record = JsonSerializer.Deserialize<SecureSessionRecord>(clearBytes, _jsonOptions);

            if (record == null ||
                string.IsNullOrWhiteSpace(record.Email) ||
                string.IsNullOrWhiteSpace(record.RefreshToken) ||
                record.RefreshTokenExpiresAt <= DateTime.UtcNow)
            {
                Clear();
                return null;
            }

            return record;
        }
        catch (CryptographicException)
        {
            Clear();
            return null;
        }
        catch (JsonException)
        {
            Clear();
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(SessionService session)
    {
        if (string.IsNullOrWhiteSpace(session.Email) ||
            string.IsNullOrWhiteSpace(session.RefreshToken) ||
            session.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_sessionPath)!;
            Directory.CreateDirectory(directory);

            var record = new SecureSessionRecord
            {
                Email = session.Email,
                RefreshToken = session.RefreshToken,
                RefreshTokenExpiresAt = session.RefreshTokenExpiresAt
            };

            var clearBytes = JsonSerializer.SerializeToUtf8Bytes(record, _jsonOptions);
            var encrypted = ProtectedData.Protect(
                clearBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            var tempPath = _sessionPath + ".tmp";
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, _sessionPath, overwrite: true);
        }
        catch (CryptographicException)
        {
            // Remember-me is a convenience feature. A storage failure must not break login.
        }
        catch (IOException)
        {
            // The active in-memory session remains valid even if persistence fails.
        }
        catch (UnauthorizedAccessException)
        {
            // The active in-memory session remains valid even if persistence fails.
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_sessionPath))
            {
                File.Delete(_sessionPath);
            }

            var tempPath = _sessionPath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
            // Logout still clears the in-memory session.
        }
        catch (UnauthorizedAccessException)
        {
            // Logout still clears the in-memory session.
        }
    }
}

public sealed class SecureSessionRecord
{
    public string Email { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
}
