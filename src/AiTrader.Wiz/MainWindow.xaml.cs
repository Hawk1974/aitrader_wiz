using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TargetAssignmentRow> _targetRows = [];
    private readonly ValidationService _validationService = new(new HttpClient());
    private WizardState _state = WizardStateFactory.CreateDefault();

    public MainWindow()
    {
        InitializeComponent();
        TargetsDataGrid.ItemsSource = _targetRows;
        InitializeOsSelectors();
        LoadStateIntoControls();
    }

    private void InitializeOsSelectors()
    {
        var source = Enum.GetValues<OperatingSystemKind>().ToList();
        Computer1OsComboBox.ItemsSource = source;
        Computer2OsComboBox.ItemsSource = source;
    }

    private void LoadStateIntoControls()
    {
        ClientNameTextBox.Text = _state.ClientIdentity.ClientName;
        MainContactNameTextBox.Text = _state.ClientIdentity.MainContactName;
        MainContactEmailTextBox.Text = _state.ClientIdentity.MainContactEmail;
        DeploymentNameTextBox.Text = _state.ClientIdentity.DeploymentName;

        ComputerCountComboBox.SelectedIndex = _state.Computers.Count == 1 ? 0 : 1;
        if (_state.Computers.Count > 0)
        {
            Computer1LabelTextBox.Text = _state.Computers[0].Label;
            Computer1OsComboBox.SelectedItem = _state.Computers[0].OperatingSystem;
            Computer1WslCheckBox.IsChecked = _state.Computers[0].UsesWslBackend;
        }

        if (_state.Computers.Count > 1)
        {
            Computer2LabelTextBox.Text = _state.Computers[1].Label;
            Computer2OsComboBox.SelectedItem = _state.Computers[1].OperatingSystem;
            Computer2WslCheckBox.IsChecked = _state.Computers[1].UsesWslBackend;
        }

        TailnetNameTextBox.Text = _state.Connectivity.TailnetName;
        BackendHostTextBox.Text = _state.Connectivity.BackendTargetHostOrIp;
        DesktopHostTextBox.Text = _state.Connectivity.DesktopTargetHostOrIp;
        SshUserTextBox.Text = _state.Connectivity.SshUsername;
        SshPortTextBox.Text = _state.Connectivity.SshPort.ToString();
        RequiresTailscaleCheckBox.IsChecked = _state.Connectivity.RequiresTailscale;
        BootstrapCompleteCheckBox.IsChecked = _state.Connectivity.BootstrapComplete;

        HermesHomeTextBox.Text = _state.Backend.HermesHome;
        RepoPathTextBox.Text = _state.Backend.RepoPath;
        DataPathTextBox.Text = _state.Backend.DataPath;
        AlTraderDataPathTextBox.Text = _state.Backend.AlTraderDataPath;
        ModelsPathTextBox.Text = _state.Backend.ModelsPath;
        LogsPathTextBox.Text = _state.Backend.LogsPath;
        BackupsPathTextBox.Text = _state.Backend.BackupsPath;
        TimezoneTextBox.Text = _state.Backend.Timezone;

        LmStudioBaseUrlTextBox.Text = _state.LmStudio.BaseUrl;
        LmStudioModelIdTextBox.Text = _state.LmStudio.ModelId;
        AlpacaPaperAccountTextBox.Text = _state.AlpacaPaper.AccountName;
        AlpacaPaperApiKeyTextBox.Text = _state.AlpacaPaper.ApiKey;
        AlpacaPaperSecretTextBox.Text = _state.AlpacaPaper.SecretKey;
        AlpacaPaperBaseUrlTextBox.Text = _state.AlpacaPaper.BaseUrl;
        TelegramBotTokenTextBox.Text = _state.Telegram.BotToken;
        TelegramChatIdTextBox.Text = _state.Telegram.ChatId;
        AgentMailApiKeyTextBox.Text = _state.AgentMail.ApiKey;
        AgentMailFromIdTextBox.Text = _state.AgentMail.FromId;
        AgentMailRecipientTextBox.Text = _state.AgentMail.RecipientEmail;
        AlpacaLiveEnabledCheckBox.IsChecked = _state.AlpacaLive.Enabled;
        AlpacaLiveApiKeyTextBox.Text = _state.AlpacaLive.ApiKey;
        AlpacaLiveSecretTextBox.Text = _state.AlpacaLive.SecretKey;
        AlpacaLiveBaseUrlTextBox.Text = _state.AlpacaLive.BaseUrl;

        RebuildTargetRows();
        UpdateComputer2Visibility();
    }

    private void PopulateStateFromControls()
    {
        _state.ClientIdentity.ClientName = ClientNameTextBox.Text.Trim();
        _state.ClientIdentity.MainContactName = MainContactNameTextBox.Text.Trim();
        _state.ClientIdentity.MainContactEmail = MainContactEmailTextBox.Text.Trim();
        _state.ClientIdentity.DeploymentName = DeploymentNameTextBox.Text.Trim();

        _state.Computers = BuildComputersFromControls();
        _state.Targets = BuildTargetsFromRows();

        _state.Connectivity.TailnetName = TailnetNameTextBox.Text.Trim();
        _state.Connectivity.BackendTargetHostOrIp = BackendHostTextBox.Text.Trim();
        _state.Connectivity.DesktopTargetHostOrIp = DesktopHostTextBox.Text.Trim();
        _state.Connectivity.SshUsername = SshUserTextBox.Text.Trim();
        _state.Connectivity.SshPort = int.TryParse(SshPortTextBox.Text.Trim(), out var sshPort) ? sshPort : 22;
        _state.Connectivity.RequiresTailscale = RequiresTailscaleCheckBox.IsChecked == true;
        _state.Connectivity.BootstrapComplete = BootstrapCompleteCheckBox.IsChecked == true;

        _state.Backend.HermesHome = HermesHomeTextBox.Text.Trim();
        _state.Backend.RepoPath = RepoPathTextBox.Text.Trim();
        _state.Backend.DataPath = DataPathTextBox.Text.Trim();
        _state.Backend.AlTraderDataPath = AlTraderDataPathTextBox.Text.Trim();
        _state.Backend.ModelsPath = ModelsPathTextBox.Text.Trim();
        _state.Backend.LogsPath = LogsPathTextBox.Text.Trim();
        _state.Backend.BackupsPath = BackupsPathTextBox.Text.Trim();
        _state.Backend.Timezone = TimezoneTextBox.Text.Trim();

        _state.LmStudio.BaseUrl = LmStudioBaseUrlTextBox.Text.Trim();
        _state.LmStudio.ModelId = LmStudioModelIdTextBox.Text.Trim();
        _state.AlpacaPaper.AccountName = AlpacaPaperAccountTextBox.Text.Trim();
        _state.AlpacaPaper.ApiKey = AlpacaPaperApiKeyTextBox.Text.Trim();
        _state.AlpacaPaper.SecretKey = AlpacaPaperSecretTextBox.Text.Trim();
        _state.AlpacaPaper.BaseUrl = AlpacaPaperBaseUrlTextBox.Text.Trim();
        _state.Telegram.BotToken = TelegramBotTokenTextBox.Text.Trim();
        _state.Telegram.ChatId = TelegramChatIdTextBox.Text.Trim();
        _state.AgentMail.ApiKey = AgentMailApiKeyTextBox.Text.Trim();
        _state.AgentMail.FromId = AgentMailFromIdTextBox.Text.Trim();
        _state.AgentMail.RecipientEmail = AgentMailRecipientTextBox.Text.Trim();
        _state.AlpacaLive.Enabled = AlpacaLiveEnabledCheckBox.IsChecked == true;
        _state.AlpacaLive.ApiKey = AlpacaLiveApiKeyTextBox.Text.Trim();
        _state.AlpacaLive.SecretKey = AlpacaLiveSecretTextBox.Text.Trim();
        _state.AlpacaLive.BaseUrl = AlpacaLiveBaseUrlTextBox.Text.Trim();
    }

    private List<ComputerDefinition> BuildComputersFromControls()
    {
        var results = new List<ComputerDefinition>
        {
            new()
            {
                Id = "computer_1",
                Label = string.IsNullOrWhiteSpace(Computer1LabelTextBox.Text) ? "Computer 1" : Computer1LabelTextBox.Text.Trim(),
                OperatingSystem = Computer1OsComboBox.SelectedItem is OperatingSystemKind os1 ? os1 : OperatingSystemKind.Windows,
                UsesWslBackend = Computer1WslCheckBox.IsChecked == true,
            }
        };

        if (ComputerCountComboBox.SelectedIndex == 1)
        {
            results.Add(new ComputerDefinition
            {
                Id = "computer_2",
                Label = string.IsNullOrWhiteSpace(Computer2LabelTextBox.Text) ? "Computer 2" : Computer2LabelTextBox.Text.Trim(),
                OperatingSystem = Computer2OsComboBox.SelectedItem is OperatingSystemKind os2 ? os2 : OperatingSystemKind.Linux,
                UsesWslBackend = Computer2WslCheckBox.IsChecked == true,
            });
        }

        return results;
    }

    private void RebuildTargetRows()
    {
        var currentSelections = _targetRows.ToDictionary(
            row => row.TargetId,
            row => row);

        var derivedTargets = TopologyService.DeriveTargets(BuildComputersFromControls());
        _targetRows.Clear();
        foreach (var target in derivedTargets)
        {
            currentSelections.TryGetValue(target.Id, out var existing);
            _targetRows.Add(new TargetAssignmentRow
            {
                TargetId = target.Id,
                DisplayName = target.DisplayName,
                Kind = target.Kind,
                HermesBackend = existing?.HermesBackend ?? false,
                HermesDesktop = existing?.HermesDesktop ?? false,
                LmStudio = existing?.LmStudio ?? false,
                IsPrimaryDesktop = existing?.IsPrimaryDesktop ?? false,
                IsAuthoritativeBackend = existing?.IsAuthoritativeBackend ?? false,
            });
        }
    }

    private List<RuntimeTarget> BuildTargetsFromRows()
    {
        return _targetRows.Select(row =>
        {
            var roles = new List<RoleKind>();
            if (row.HermesBackend) roles.Add(RoleKind.HermesBackend);
            if (row.HermesDesktop) roles.Add(RoleKind.HermesDesktop);
            if (row.LmStudio) roles.Add(RoleKind.LmStudio);

            return new RuntimeTarget
            {
                Id = row.TargetId,
                DisplayName = row.DisplayName,
                Kind = row.Kind,
                ComputerId = row.TargetId.Split('-')[0],
                Roles = roles,
                IsPrimaryDesktop = row.IsPrimaryDesktop,
                IsAuthoritativeBackend = row.IsAuthoritativeBackend,
            };
        }).ToList();
    }

    private void ComputerCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateComputer2Visibility();
        RebuildTargetRows();
    }

    private void ComputerOsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateWslCheckboxVisibility();
        RebuildTargetRows();
    }

    private void WslCheckBox_OnChanged(object sender, RoutedEventArgs e) => RebuildTargetRows();

    private void DeriveTargetsButton_OnClick(object sender, RoutedEventArgs e)
    {
        RebuildTargetRows();
        AppendLog("Runtime targets re-derived from the current computer selections.");
        WizardTabs.SelectedIndex = 2;
    }

    private async void RunValidationsButton_OnClick(object sender, RoutedEventArgs e)
    {
        PopulateStateFromControls();
        _state.ValidationResults.Clear();
        _state.ValidationResults.Add(ValidationService.ClassifyTopology(_state));
        _state.ValidationResults.Add(_validationService.ValidateConnectivity(_state.Connectivity));
        _state.ValidationResults.Add(await _validationService.ValidateAlpacaPaperAsync(_state.AlpacaPaper));
        _state.ValidationResults.Add(await _validationService.ValidateTelegramAsync(_state.Telegram));
        _state.ValidationResults.Add(await _validationService.ValidateAgentMailAsync(_state.AgentMail));
        _state.ValidationResults.Add(await _validationService.ValidateLmStudioAsync(_state.LmStudio));
        _state.ValidationResults.Add(await _validationService.ValidateAlpacaLiveAsync(_state.AlpacaLive));

        AppendLog("Validation results:");
        foreach (var record in _state.ValidationResults)
        {
            AppendLog($"- {record.Key}: {record.Status} - {record.Message}");
        }
    }

    private void SaveDraftButton_OnClick(object sender, RoutedEventArgs e)
    {
        PopulateStateFromControls();
        DraftStorage.Save(_state);
        AppendLog("Encrypted local draft saved.");
    }

    private void LoadDraftButton_OnClick(object sender, RoutedEventArgs e)
    {
        var loaded = DraftStorage.Load();
        if (loaded is null)
        {
            AppendLog("No saved draft was found.");
            return;
        }

        _state = loaded;
        LoadStateIntoControls();
        AppendLog("Encrypted local draft loaded.");
    }

    private void WipeDraftButton_OnClick(object sender, RoutedEventArgs e)
    {
        DraftStorage.Wipe();
        AppendLog("Local draft wiped.");
    }

    private void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        PopulateStateFromControls();
        var topologyErrors = TopologyService.ValidateTopology(_state);
        if (topologyErrors.Count > 0)
        {
            AppendLog("Export blocked:");
            foreach (var error in topologyErrors)
            {
                AppendLog($"- {error}");
            }
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export AI overlay zip",
            FileName = "AiTraderWiz_Overlay.zip",
            Filter = "Zip Files (*.zip)|*.zip",
            DefaultExt = ".zip",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ExportService.ExportOverlayZip(_state, dialog.FileName);
        AppendLog($"Overlay files exported to: {dialog.FileName}");
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (WizardTabs.SelectedIndex > 0)
        {
            WizardTabs.SelectedIndex -= 1;
        }
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (WizardTabs.SelectedIndex < WizardTabs.Items.Count - 1)
        {
            WizardTabs.SelectedIndex += 1;
        }
    }

    private void UpdateComputer2Visibility()
    {
        Computer2GroupBox.Visibility = ComputerCountComboBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWslCheckboxVisibility()
    {
        Computer1WslCheckBox.Visibility = Computer1OsComboBox.SelectedItem is OperatingSystemKind.Windows ? Visibility.Visible : Visibility.Collapsed;
        Computer2WslCheckBox.Visibility = Computer2OsComboBox.SelectedItem is OperatingSystemKind.Windows ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AppendLog(string message)
    {
        var current = LogTextBox.Text;
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(current))
        {
            builder.AppendLine(current.TrimEnd());
        }

        builder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogTextBox.Text = builder.ToString();
        LogTextBox.ScrollToEnd();
    }
}
