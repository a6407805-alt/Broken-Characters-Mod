// ============================================================================================================
//  Password-locked keys. Instead of pasting a long github_pat_... key, a friend types a password: the launcher
//  downloads the locked copy of the key from the repo and unlocks it on their PC.
//
//    launcher/keys/extras.json             the extras key, locked with the extras password (set by the owner)
//
//  The files are public, so the lock has to hold up on its own: AES-256-GCM with a key made from the password by
//  PBKDF2-SHA256 (600,000 rounds, random salt). A long password matters; the window asks for 8+ characters.
// ============================================================================================================

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BrokenCharactersLauncher
{
    public static class KeyVault
    {
        private const int Iterations = 600_000;
        public const int MinPasswordLength = 8;
        public const string ExtrasPath = "launcher/keys/extras.json";
        /// <summary>The friends' shared upload key (for silent staging), locked with the EXTRAS KEY itself, so only
        /// PCs that have the extras can unlock it.</summary>
        public const string StagingPath = "launcher/keys/staging.json";

        public static string? ReadExtrasKey()
        {
            string f = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters", "extras-key.txt");
            try { return System.IO.File.Exists(f) ? System.IO.File.ReadAllText(f).Trim() : null; }
            catch (System.IO.IOException) { return null; }
        }
        private const string ExtrasRepo = "a6407805-alt/BrokenCharacters-Extras";

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BrokenCharactersLauncher", "1.0"));
            return c;
        }

        /// <summary>True for a real GitHub key; anything else typed into a key box is treated as a password.</summary>
        public static bool LooksLikeKey(string text) =>
            text.StartsWith("github_pat_", StringComparison.Ordinal) || text.StartsWith("ghp_", StringComparison.Ordinal);

        public static string Lock(string secret, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            byte[] plain = Encoding.UTF8.GetBytes(secret);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[16];
            using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag);
            return new JsonObject
            {
                ["v"] = 1,
                ["iterations"] = Iterations,
                ["salt"] = Convert.ToBase64String(salt),
                ["nonce"] = Convert.ToBase64String(nonce),
                ["data"] = Convert.ToBase64String(cipher),
                ["tag"] = Convert.ToBase64String(tag),
            }.ToJsonString() + "\n";
        }

        /// <summary>The key inside, or null if the password is wrong.</summary>
        public static string? Unlock(string lockedJson, string password)
        {
            try
            {
                JsonNode n = JsonNode.Parse(lockedJson)!;
                byte[] salt = Convert.FromBase64String(n["salt"]!.GetValue<string>());
                byte[] nonce = Convert.FromBase64String(n["nonce"]!.GetValue<string>());
                byte[] cipher = Convert.FromBase64String(n["data"]!.GetValue<string>());
                byte[] tag = Convert.FromBase64String(n["tag"]!.GetValue<string>());
                int rounds = n["iterations"]?.GetValue<int>() ?? Iterations;
                byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, 32);
                byte[] plain = new byte[cipher.Length];
                using (var aes = new AesGcm(key, 16)) aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException) { return null; }               // wrong password
            catch (Exception ex) when (ex is FormatException || ex is NullReferenceException || ex is System.Text.Json.JsonException) { return null; }
        }

        /// <summary>Downloads a locked key file from the repo (public). Null if it doesn't exist (no password set up yet).</summary>
        public static async Task<string?> FetchAsync(string repoPath, CancellationToken ct = default)
        {
            string url = "https://raw.githubusercontent.com/" + Publisher.Repo + "/main/" + repoPath + "?t=" + DateTime.UtcNow.Ticks;
            using HttpResponseMessage resp = await Http.GetAsync(url, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct);
        }

        /// <summary>Unlocks with a password in one go. Returns (key, null) or (null, a message to show).</summary>
        public static async Task<(string? Key, string? Error)> UnlockFromRepoAsync(string repoPath, string password, CancellationToken ct = default)
        {
            string? locked;
            try { locked = await FetchAsync(repoPath, ct); }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return (null, "Couldn't reach GitHub. Check your internet and try again.");
            }
            if (locked == null) return (null, "No password has been set up for that yet. Ask the pack owner.");
            string? key = await Task.Run(() => Unlock(locked, password), ct);
            return key == null ? (null, "Wrong password.") : (key, null);
        }

        /// <summary>Checks an extras key can actually read the private extras repo before it gets locked.</summary>
        public static async Task<bool> ExtrasKeyWorksAsync(string key, CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + ExtrasRepo);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using HttpResponseMessage resp = await Http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
    }
}
