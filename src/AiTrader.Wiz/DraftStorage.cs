using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public static class DraftStorage
{
    private static readonly string DraftDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiTraderWiz");

    private static readonly string DraftPath = Path.Combine(DraftDirectory, "wizard_draft.bin");

    public static void Save(WizardState state)
    {
        Directory.CreateDirectory(DraftDirectory);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        var plaintext = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(DraftPath, protectedBytes);
    }

    public static WizardState? Load()
    {
        if (!File.Exists(DraftPath))
        {
            return null;
        }

        var protectedBytes = File.ReadAllBytes(DraftPath);
        var plaintext = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(plaintext);
        return JsonSerializer.Deserialize<WizardState>(json);
    }

    public static void Wipe()
    {
        if (File.Exists(DraftPath))
        {
            File.Delete(DraftPath);
        }
    }
}
