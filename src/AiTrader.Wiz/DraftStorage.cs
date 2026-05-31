using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public static class DraftStorage
{
    private const string ApplicationDataFolderName = "AlTraderConfigWizard";
    private static readonly string DraftDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationDataFolderName);

    private static readonly string DraftPath = Path.Combine(DraftDirectory, "wizard_draft.bin");

    public static void Save(WizardState state)
    {
        VerboseLogger.Info("DraftStorage.Save invoked.");
        Directory.CreateDirectory(DraftDirectory);
        var draftSafeState = WizardStateSanitizer.CreateDraftSafeCopy(state);
        var json = JsonSerializer.Serialize(draftSafeState, new JsonSerializerOptions { WriteIndented = true });
        var plaintext = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(DraftPath, protectedBytes);
        VerboseLogger.Info($"Non-secret draft saved to {DraftPath}.");
    }

    public static WizardState? Load()
    {
        VerboseLogger.Info("DraftStorage.Load invoked.");
        if (!File.Exists(DraftPath))
        {
            VerboseLogger.Warn($"Draft load requested but no file exists at {DraftPath}.");
            return null;
        }

        var protectedBytes = File.ReadAllBytes(DraftPath);
        var plaintext = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(plaintext);
        var state = JsonSerializer.Deserialize<WizardState>(json);
        VerboseLogger.Info($"Draft loaded from {DraftPath}. NullState={state is null}.");
        return state;
    }

    public static void Wipe()
    {
        VerboseLogger.Info("DraftStorage.Wipe invoked.");
        if (File.Exists(DraftPath))
        {
            File.Delete(DraftPath);
            VerboseLogger.Info($"Draft file deleted at {DraftPath}.");
            return;
        }

        VerboseLogger.Warn($"Draft wipe requested but no file exists at {DraftPath}.");
    }
}
