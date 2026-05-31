using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public sealed class ApprovalRecord
{
    public string TimestampUtc { get; set; } = string.Empty;
    public string WindowsUser { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string SummarySha256 { get; set; } = string.Empty;
}

public static class ApprovalHistoryStorage
{
    private const string ProductFolderName = "AlTrader";
    private const string ApplicationFolderName = "ConfigWizard";
    private const string ApprovalFileName = "approval_history.jsonl";
    private static readonly string ApprovalDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductFolderName,
        ApplicationFolderName);
    private static readonly string ApprovalPath = Path.Combine(ApprovalDirectory, ApprovalFileName);

    public static ApprovalRecord RecordApproval(WizardState state, string summaryText)
    {
        Directory.CreateDirectory(ApprovalDirectory);
        var record = new ApprovalRecord
        {
            TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            WindowsUser = Environment.UserName,
            ClientName = state.ClientIdentity.ClientName,
            DeploymentName = state.ClientIdentity.DeploymentName,
            SummarySha256 = ComputeSha256(summaryText),
        };

        var json = JsonSerializer.Serialize(record);
        File.AppendAllText(ApprovalPath, json + Environment.NewLine, Encoding.UTF8);
        VerboseLogger.Info($"Approval recorded at {ApprovalPath} for deployment {record.DeploymentName}.");
        return record;
    }

    public static string LoadHistoryDisplayText()
    {
        if (!File.Exists(ApprovalPath))
        {
            return "No local approval history has been recorded yet.";
        }

        var lines = File.ReadAllLines(ApprovalPath, Encoding.UTF8)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<ApprovalRecord>(line))
            .Where(record => record is not null)
            .Select(record => $"{record!.TimestampUtc} | User={record.WindowsUser} | Client={record.ClientName} | Deployment={record.DeploymentName} | SummarySha256={record.SummarySha256}")
            .ToList();

        return lines.Count == 0
            ? "No local approval history has been recorded yet."
            : string.Join(Environment.NewLine, lines);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
