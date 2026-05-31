using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TargetAssignmentRow> _targetRows = [];
    private readonly ObservableCollection<ComputerServicePlacementRow> _computer1ServiceRows = [];
    private readonly ObservableCollection<ComputerServicePlacementRow> _computer2ServiceRows = [];
    private readonly ValidationService _validationService = new(new HttpClient());
    private WizardState _state = WizardStateFactory.CreateDefault();
    private bool _isSynchronizingCashInputs;
    private bool _isLoadingState;
    private bool _uiInitialized;
    private bool _hasPassingFullValidation;
    private bool _summaryApproved;
    private int _lastWizardTabIndex;

    public MainWindow()
    {
        VerboseLogger.Info("MainWindow constructor entered.");
        InitializeComponent();
        RegisterGlobalChangeTracking();
        InitializeWindowIcon();
        TargetsDataGrid.ItemsSource = _targetRows;
        Computer1ServicePlacementDataGrid.ItemsSource = _computer1ServiceRows;
        Computer2ServicePlacementDataGrid.ItemsSource = _computer2ServiceRows;
        InitializeOsSelectors();
        LoadStateIntoControls();
        _uiInitialized = true;
        _lastWizardTabIndex = WizardTabs.SelectedIndex;
        UpdateApprovalAndExportUi();
        AppendLog($"Verbose log file: {VerboseLogger.CurrentLogDisplayPath}");
        VerboseLogger.Info("MainWindow constructor completed.");
    }

    private void RegisterGlobalChangeTracking()
    {
        AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(AnyWizardTextChanged));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(AnyWizardSelectionChanged));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(AnyWizardToggleChanged));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(AnyWizardToggleChanged));
        AddHandler(SecretFieldControl.ValueChangedEvent, new RoutedEventHandler(AnyWizardToggleChanged));
    }

    private void InitializeWindowIcon()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            VerboseLogger.Warn("Window icon initialization skipped because Environment.ProcessPath was empty.");
            return;
        }

        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
        if (icon is null)
        {
            VerboseLogger.Warn($"Window icon initialization skipped because no associated icon was found for {executablePath}.");
            return;
        }

        Icon = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        VerboseLogger.Info($"Window icon loaded from executable association at {executablePath}.");
    }

    private void InitializeOsSelectors()
    {
        VerboseLogger.Info("Initializing operating system selector sources.");
        var source = Enum.GetValues<OperatingSystemKind>().ToList();
        Computer1OsComboBox.ItemsSource = source;
        Computer2OsComboBox.ItemsSource = source;
        AiProviderComboBox.ItemsSource = HermesProviderCatalog.All;
        AiProviderServicesComboBox.ItemsSource = HermesProviderCatalog.All;
        Computer1AccessModeComboBox.ItemsSource = DeploymentCatalog.AccessModeOptions;
        Computer2AccessModeComboBox.ItemsSource = DeploymentCatalog.AccessModeOptions;
    }

    private void LoadStateIntoControls()
    {
        VerboseLogger.Info("Loading wizard state into UI controls.");
        _isLoadingState = true;
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
            Computer1DockerCheckBox.IsChecked = _state.Computers[0].DockerAvailable;
            Computer1AccessModeComboBox.SelectedValue = _state.Computers[0].AccessMode;
            Computer1AccessHostTextBox.Text = _state.Computers[0].AccessHostOrIp;
            Computer1AccessUsernameTextBox.Text = _state.Computers[0].AccessUsername;
            Computer1AccessPortTextBox.Text = _state.Computers[0].AccessPort.ToString(CultureInfo.InvariantCulture);
        }

        if (_state.Computers.Count > 1)
        {
            Computer2LabelTextBox.Text = _state.Computers[1].Label;
            Computer2OsComboBox.SelectedItem = _state.Computers[1].OperatingSystem;
            Computer2WslCheckBox.IsChecked = _state.Computers[1].UsesWslBackend;
            Computer2DockerCheckBox.IsChecked = _state.Computers[1].DockerAvailable;
            Computer2AccessModeComboBox.SelectedValue = _state.Computers[1].AccessMode;
            Computer2AccessHostTextBox.Text = _state.Computers[1].AccessHostOrIp;
            Computer2AccessUsernameTextBox.Text = _state.Computers[1].AccessUsername;
            Computer2AccessPortTextBox.Text = _state.Computers[1].AccessPort.ToString(CultureInfo.InvariantCulture);
        }

        TailnetNameTextBox.Text = _state.Connectivity.TailnetName;
        BackendHostTextBox.Text = _state.Connectivity.BackendTargetHostOrIp;
        DesktopHostTextBox.Text = _state.Connectivity.DesktopTargetHostOrIp;
        SshUserTextBox.Text = _state.Connectivity.SshUsername;
        SshPortTextBox.Text = _state.Connectivity.SshPort.ToString(CultureInfo.InvariantCulture);
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

        var selectedProvider = HermesProviderCatalog.Find(_state.HermesAiProvider.ProviderKey);
        AiProviderComboBox.SelectedItem = selectedProvider;
        AiProviderServicesComboBox.SelectedItem = selectedProvider;
        AiProviderBaseUrlTextBox.Text = _state.HermesAiProvider.BaseUrl;
        AiProviderModelTextBox.Text = _state.HermesAiProvider.ModelName;
        AiProviderApiKeyControl.Value = _state.HermesAiProvider.ApiKey;
        AlpacaPaperAccountTextBox.Text = _state.AlpacaPaper.AccountName;
        AlpacaPaperApiKeyControl.Value = _state.AlpacaPaper.ApiKey;
        AlpacaPaperSecretControl.Value = _state.AlpacaPaper.SecretKey;
        AlpacaPaperBaseUrlTextBox.Text = _state.AlpacaPaper.BaseUrl;
        TelegramBotTokenControl.Value = _state.Telegram.BotToken;
        TelegramChatIdTextBox.Text = _state.Telegram.ChatId;
        AgentMailApiKeyControl.Value = _state.AgentMail.ApiKey;
        AgentMailFromIdTextBox.Text = _state.AgentMail.FromId;
        AgentMailRecipientTextBox.Text = _state.AgentMail.RecipientEmail;
        AlpacaLiveEnabledCheckBox.IsChecked = _state.AlpacaLive.Enabled;
        AlpacaLiveApiKeyControl.Value = _state.AlpacaLive.ApiKey;
        AlpacaLiveSecretControl.Value = _state.AlpacaLive.SecretKey;
        AlpacaLiveBaseUrlTextBox.Text = _state.AlpacaLive.BaseUrl;

        LoadCashAllocationIntoControls();
        RebuildTargetRows();
        LoadServicePlacementRows();
        UpdateComputer2Visibility();
        UpdateWslCheckboxVisibility();
        UpdateAccessModeVisibility();
        UpdateServicePlacementVisibility();
        UpdateAiProviderUi();
        UpdateServiceTestButtonState();
        UpdateApprovalHistoryDisplay();
        RefreshApprovalSummary();
        _isLoadingState = false;
        MarkValidationStateDirty(logChange: false);
        VerboseLogger.Info("Wizard state loaded into UI controls.");
    }

    private void LoadCashAllocationIntoControls()
    {
        _isSynchronizingCashInputs = true;
        CashBasisTextBox.Text = _state.CashAllocation.StartOfDayCashBasis.ToString("0.##", CultureInfo.InvariantCulture);
        SetAllocationControl(_state.CashAllocation.ProtectedReserve, ProtectedReservePercentRadio, ProtectedReserveDollarRadio, ProtectedReserveValueTextBox);
        SetAllocationControl(_state.CashAllocation.PerTickerAllocation, PerTickerPercentRadio, PerTickerDollarRadio, PerTickerValueTextBox);
        MaxRankedCandidatesTextBox.Text = _state.CashAllocation.MaxRankedCandidatesPerCycle.ToString(CultureInfo.InvariantCulture);
        _isSynchronizingCashInputs = false;
        UpdateCashAllocationHelperText();
    }

    private static void SetAllocationControl(
        AllocationInput input,
        ToggleButton percentRadio,
        ToggleButton dollarRadio,
        TextBox valueTextBox)
    {
        percentRadio.IsChecked = input.Mode == AllocationEntryMode.Percent;
        dollarRadio.IsChecked = input.Mode == AllocationEntryMode.Dollar;
        valueTextBox.Text = input.Value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void PopulateStateFromControls()
    {
        VerboseLogger.Info("Populating wizard state from UI controls.");
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

        _state.HermesAiProvider.ProviderKey = GetSelectedProviderKey();
        _state.HermesAiProvider.BaseUrl = AiProviderBaseUrlTextBox.Text.Trim();
        _state.HermesAiProvider.ModelName = AiProviderModelTextBox.Text.Trim();
        _state.HermesAiProvider.ApiKey = AiProviderApiKeyControl.Value.Trim();
        _state.AlpacaPaper.AccountName = AlpacaPaperAccountTextBox.Text.Trim();
        _state.AlpacaPaper.ApiKey = AlpacaPaperApiKeyControl.Value.Trim();
        _state.AlpacaPaper.SecretKey = AlpacaPaperSecretControl.Value.Trim();
        _state.AlpacaPaper.BaseUrl = AlpacaPaperBaseUrlTextBox.Text.Trim();
        _state.Telegram.BotToken = TelegramBotTokenControl.Value.Trim();
        _state.Telegram.ChatId = TelegramChatIdTextBox.Text.Trim();
        _state.AgentMail.ApiKey = AgentMailApiKeyControl.Value.Trim();
        _state.AgentMail.FromId = AgentMailFromIdTextBox.Text.Trim();
        _state.AgentMail.RecipientEmail = AgentMailRecipientTextBox.Text.Trim();
        _state.AlpacaLive.Enabled = AlpacaLiveEnabledCheckBox.IsChecked == true;
        _state.AlpacaLive.ApiKey = AlpacaLiveApiKeyControl.Value.Trim();
        _state.AlpacaLive.SecretKey = AlpacaLiveSecretControl.Value.Trim();
        _state.AlpacaLive.BaseUrl = AlpacaLiveBaseUrlTextBox.Text.Trim();

        _state.CashAllocation.StartOfDayCashBasis = ParseDecimalOrDefault(CashBasisTextBox.Text, 25000m);
        _state.CashAllocation.ProtectedReserve = BuildAllocationInput(ProtectedReservePercentRadio, ProtectedReserveValueTextBox, 10m);
        _state.CashAllocation.PerTickerAllocation = BuildAllocationInput(PerTickerPercentRadio, PerTickerValueTextBox, 10m);
        _state.CashAllocation.MaxRankedCandidatesPerCycle = ParseIntOrDefault(MaxRankedCandidatesTextBox.Text, 5);
        VerboseLogger.Info($"Wizard state populated. Computers={_state.Computers.Count}, Targets={_state.Targets.Count}, LiveEnabled={_state.AlpacaLive.Enabled}.");
    }

    private static AllocationInput BuildAllocationInput(ToggleButton percentRadio, TextBox valueTextBox, decimal defaultValue)
    {
        if (percentRadio is null || valueTextBox is null)
        {
            return new AllocationInput
            {
                Mode = AllocationEntryMode.Percent,
                Value = defaultValue,
            };
        }

        return new AllocationInput
        {
            Mode = percentRadio.IsChecked == true ? AllocationEntryMode.Percent : AllocationEntryMode.Dollar,
            Value = ParseDecimalOrDefault(valueTextBox.Text, defaultValue),
        };
    }

    private List<ComputerDefinition> BuildComputersFromControls()
    {
        if (Computer1LabelTextBox is null ||
            Computer1OsComboBox is null ||
            Computer1WslCheckBox is null ||
            ComputerCountComboBox is null)
        {
            VerboseLogger.Warn("BuildComputersFromControls invoked before primary controls were initialized. Falling back to current state.");
            return _state.Computers
                .Select(computer => new ComputerDefinition
                {
                    Id = computer.Id,
                    Label = computer.Label,
                    OperatingSystem = computer.OperatingSystem,
                    UsesWslBackend = computer.UsesWslBackend,
                    DockerAvailable = computer.DockerAvailable,
                    AccessMode = computer.AccessMode,
                    AccessHostOrIp = computer.AccessHostOrIp,
                    AccessPort = computer.AccessPort,
                    AccessUsername = computer.AccessUsername,
                    ServicePlacements = computer.ServicePlacements.Select(item => new ServicePlacement
                    {
                        Service = item.Service,
                        PlacementMode = item.PlacementMode,
                    }).ToList(),
                })
                .ToList();
        }

        var results = new List<ComputerDefinition>
        {
            new()
            {
                Id = "computer_1",
                Label = string.IsNullOrWhiteSpace(Computer1LabelTextBox.Text) ? "Computer 1" : Computer1LabelTextBox.Text.Trim(),
                OperatingSystem = Computer1OsComboBox.SelectedItem is OperatingSystemKind os1 ? os1 : OperatingSystemKind.Windows,
                UsesWslBackend = Computer1WslCheckBox.IsChecked == true,
                DockerAvailable = Computer1DockerCheckBox.IsChecked == true,
                AccessMode = Computer1AccessModeComboBox.SelectedValue is AccessMode accessMode1 ? accessMode1 : AccessMode.DirectLocal,
                AccessHostOrIp = Computer1AccessHostTextBox.Text.Trim(),
                AccessUsername = Computer1AccessUsernameTextBox.Text.Trim(),
                AccessPort = ParseIntOrDefault(Computer1AccessPortTextBox.Text, 22),
                ServicePlacements = BuildServicePlacements(_computer1ServiceRows),
            }
        };

        if (ComputerCountComboBox.SelectedIndex == 1)
        {
            if (Computer2LabelTextBox is null || Computer2OsComboBox is null || Computer2WslCheckBox is null)
            {
                VerboseLogger.Warn("Computer 2 was requested before its controls were initialized. Falling back to current secondary computer state.");
                var existingComputer2 = _state.Computers.Skip(1).FirstOrDefault();
                if (existingComputer2 is not null)
                {
                    results.Add(new ComputerDefinition
                    {
                        Id = existingComputer2.Id,
                        Label = existingComputer2.Label,
                        OperatingSystem = existingComputer2.OperatingSystem,
                        UsesWslBackend = existingComputer2.UsesWslBackend,
                        DockerAvailable = existingComputer2.DockerAvailable,
                        AccessMode = existingComputer2.AccessMode,
                        AccessHostOrIp = existingComputer2.AccessHostOrIp,
                        AccessPort = existingComputer2.AccessPort,
                        AccessUsername = existingComputer2.AccessUsername,
                        ServicePlacements = existingComputer2.ServicePlacements.Select(item => new ServicePlacement
                        {
                            Service = item.Service,
                            PlacementMode = item.PlacementMode,
                        }).ToList(),
                    });
                }

                return results;
            }

            results.Add(new ComputerDefinition
            {
                Id = "computer_2",
                Label = string.IsNullOrWhiteSpace(Computer2LabelTextBox.Text) ? "Computer 2" : Computer2LabelTextBox.Text.Trim(),
                OperatingSystem = Computer2OsComboBox.SelectedItem is OperatingSystemKind os2 ? os2 : OperatingSystemKind.Linux,
                UsesWslBackend = Computer2WslCheckBox.IsChecked == true,
                DockerAvailable = Computer2DockerCheckBox.IsChecked == true,
                AccessMode = Computer2AccessModeComboBox.SelectedValue is AccessMode accessMode2 ? accessMode2 : AccessMode.DirectLocal,
                AccessHostOrIp = Computer2AccessHostTextBox.Text.Trim(),
                AccessUsername = Computer2AccessUsernameTextBox.Text.Trim(),
                AccessPort = ParseIntOrDefault(Computer2AccessPortTextBox.Text, 22),
                ServicePlacements = BuildServicePlacements(_computer2ServiceRows),
            });
        }

        return results;
    }

    private void RebuildTargetRows()
    {
        VerboseLogger.Info("Rebuilding runtime target rows.");
        var currentSelections = _targetRows.ToDictionary(
            row => row.TargetId,
            row => row);

        var computers = BuildComputersFromControls();
        var defaultState = new WizardState
        {
            Computers = computers,
            Targets = TopologyService.DeriveTargets(computers),
            HermesAiProvider = new HermesAiProviderConfiguration
            {
                ProviderKey = _state.HermesAiProvider.ProviderKey,
            }
        };
        TopologyService.ApplyDefaultTargetAssignments(defaultState);

        _targetRows.Clear();
        foreach (var target in defaultState.Targets)
        {
            currentSelections.TryGetValue(target.Id, out var existing);
            _targetRows.Add(new TargetAssignmentRow
            {
                TargetId = target.Id,
                DisplayName = target.DisplayName,
                Kind = target.Kind,
                HermesBackend = existing?.HermesBackend ?? target.Roles.Contains(RoleKind.HermesBackend),
                HermesDesktop = existing?.HermesDesktop ?? target.Roles.Contains(RoleKind.HermesDesktop),
                IsPrimaryDesktop = existing?.IsPrimaryDesktop ?? target.IsPrimaryDesktop,
                IsAuthoritativeBackend = existing?.IsAuthoritativeBackend ?? target.IsAuthoritativeBackend,
                AiProviderKey = existing?.AiProviderKey ?? _state.HermesAiProvider.ProviderKey,
            });
        }
        VerboseLogger.Info($"Runtime target rows rebuilt. Count={_targetRows.Count}.");
        SyncAiProviderSelectionFromRows();
        RefreshDeploymentModelDefaults();
    }

    private List<RuntimeTarget> BuildTargetsFromRows()
    {
        return _targetRows.Select(row =>
        {
            var roles = new List<RoleKind>();
            if (row.HermesBackend) roles.Add(RoleKind.HermesBackend);
            if (row.HermesDesktop) roles.Add(RoleKind.HermesDesktop);
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

    private async void RunValidationsButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Run Validations button clicked.");
        try
        {
            var results = await RunAllValidationsAsync();
            _hasPassingFullValidation = results.All(record => record.Status != ValidationStatus.FailedBlocking);
            _summaryApproved = false;
            RefreshApprovalSummary();
            UpdateApprovalAndExportUi();

            AppendLog("Validation results:");
            foreach (var record in results)
            {
                AppendLog($"- {record.Key}: {record.Status} - {record.Message}");
                VerboseLogger.Info($"Validation result: {record.Key} => {record.Status} :: {record.Message}");
            }
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("Validation workflow failed.", ex);
            AppendLog($"Validation failed: {ex.Message}");
            throw;
        }
    }

    private void SaveDraftButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Save Draft button clicked.");
        try
        {
            PopulateStateFromControls();
            DraftStorage.Save(_state);
            AppendLog("Encrypted local non-secret draft saved.");
            VerboseLogger.Info("Encrypted local non-secret draft saved successfully.");
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("Failed to save encrypted draft.", ex);
            AppendLog($"Draft save failed: {ex.Message}");
            throw;
        }
    }

    private void LoadDraftButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Load Draft button clicked.");
        try
        {
            var loaded = DraftStorage.Load();
            if (loaded is null)
            {
                AppendLog("No saved draft was found.");
                VerboseLogger.Warn("Load draft requested but no draft file exists.");
                return;
            }

            _state = loaded;
            LoadStateIntoControls();
            AppendLog("Encrypted local non-secret draft loaded. Secret fields were intentionally left blank.");
            VerboseLogger.Info("Encrypted local non-secret draft loaded successfully.");
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("Failed to load encrypted draft.", ex);
            AppendLog($"Draft load failed: {ex.Message}");
            throw;
        }
    }

    private void WipeDraftButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Wipe Draft button clicked.");
        try
        {
            DraftStorage.Wipe();
            _state = WizardStateFactory.CreateDefault();
            LoadStateIntoControls();
            AppendLog("Local draft wiped and current wizard values reset to defaults.");
            VerboseLogger.Info("Local draft wiped successfully.");
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("Failed to wipe local draft.", ex);
            AppendLog($"Draft wipe failed: {ex.Message}");
            throw;
        }
    }

    private void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Export button clicked.");
        try
        {
            if (!_hasPassingFullValidation)
            {
                AppendLog("Export blocked: run validations and resolve all blocking issues before export.");
                return;
            }

            if (!_summaryApproved)
            {
                AppendLog("Export blocked: approve the redacted summary on Tab 8 before export.");
                return;
            }

            PopulateStateFromControls();
            var validationResults = RunAllValidationsAsync().GetAwaiter().GetResult();
            var blocking = validationResults.Where(record => record.Status == ValidationStatus.FailedBlocking).ToList();
            if (blocking.Count > 0)
            {
                AppendLog("Export blocked:");
                foreach (var record in blocking)
                {
                    AppendLog($"- {record.Key}: {record.Message}");
                    VerboseLogger.Warn($"Export blocked by validation failure: {record.Key} => {record.Message}");
                }
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export AlTrader stand-up package zip",
                FileName = "AiTraderWiz_Standup_Package.zip",
                Filter = "Zip Files (*.zip)|*.zip",
                DefaultExt = ".zip",
            };

            if (dialog.ShowDialog(this) != true)
            {
                VerboseLogger.Info("Export dialog cancelled by user.");
                return;
            }

            ExportService.ExportStandupPackageZip(_state, dialog.FileName);
            AppendLog($"Stand-up package exported to: {dialog.FileName}");
            VerboseLogger.Info($"Stand-up package zip exported successfully to {dialog.FileName}.");
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("Stand-up package export failed.", ex);
            AppendLog($"Export failed: {ex.Message}");
            throw;
        }
    }

    private void ClearLogButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (LogTextBox is null || string.IsNullOrEmpty(LogTextBox.Text))
        {
            return;
        }

        LogTextBox.Clear();
        VerboseLogger.Info("Visible log display cleared by user.");
    }

    private void AboutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("About dialog opened.");
        var dialog = new AboutWindow
        {
            Owner = this,
        };
        dialog.ShowDialog();
        VerboseLogger.Info("About dialog closed.");
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (WizardTabs.SelectedIndex > 0)
        {
            WizardTabs.SelectedIndex -= 1;
            VerboseLogger.Info($"Navigated back to wizard tab index {WizardTabs.SelectedIndex}.");
        }
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (WizardTabs.SelectedIndex == GetReviewTabIndex() && !_hasPassingFullValidation)
        {
            AppendLog("Tab 8 remains unavailable until all validations pass without blocking issues.");
            return;
        }

        if (WizardTabs.SelectedIndex < WizardTabs.Items.Count - 1)
        {
            WizardTabs.SelectedIndex += 1;
            VerboseLogger.Info($"Navigated forward to wizard tab index {WizardTabs.SelectedIndex}.");
        }
    }

    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Cancel / Exit button clicked.");
        var result = MessageBox.Show(
            "Exit AlTrader Config Wizard without exporting files for the setup AI?",
            "Exit AlTrader Config Wizard",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            VerboseLogger.Info("Exit was cancelled by the user.");
            AppendLog("Exit cancelled. The wizard remains open.");
            return;
        }

        VerboseLogger.Warn("Application exit confirmed before stand-up package export.");
        AppendLog("Exiting without exporting stand-up package files.");
        Close();
    }

    private void ComputerCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        VerboseLogger.Info($"Computer count changed. SelectedIndex={ComputerCountComboBox?.SelectedIndex}.");
        UpdateComputer2Visibility();
        RebuildTargetRows();
    }

    private void ComputerOsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            VerboseLogger.Info($"Operating system selection changed on {comboBox.Name}. SelectedItem={comboBox.SelectedItem ?? "<null>"}.");
        }
        UpdateWslCheckboxVisibility();
        RebuildTargetRows();
    }

    private void WslCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            VerboseLogger.Info($"WSL backend checkbox changed on {checkBox.Name}. IsChecked={checkBox.IsChecked}.");
        }

        RebuildTargetRows();
    }

    private void AccessModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiInitialized)
        {
            return;
        }

        UpdateAccessModeVisibility();
    }

    private void DeriveTargetsButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Derive Targets button clicked.");
        RebuildTargetRows();
        AppendLog("Runtime targets re-derived from the current computer selections.");
        WizardTabs.SelectedIndex = 2;
    }

    private void WizardTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, WizardTabs) || e.Source != WizardTabs)
        {
            return;
        }

        if (WizardTabs.SelectedItem == ApprovalTab && !_hasPassingFullValidation)
        {
            WizardTabs.SelectedIndex = Math.Clamp(_lastWizardTabIndex, 0, WizardTabs.Items.Count - 1);
            AppendLog("Approval + Export is unavailable until validations pass without blocking issues.");
            return;
        }

        _lastWizardTabIndex = WizardTabs.SelectedIndex;
        if (WizardTabs.SelectedItem == ApprovalTab)
        {
            RefreshApprovalSummary();
            UpdateApprovalHistoryDisplay();
        }

        UpdateNavigationButtons();
    }

    private void AiProviderComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiInitialized)
        {
            return;
        }

        SyncProviderComboSelections(AiProviderComboBox);
        UpdateAiProviderUi();
        SyncAiProviderSelectionToRows();
    }

    private void AiProviderServicesComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiInitialized)
        {
            return;
        }

        SyncProviderComboSelections(AiProviderServicesComboBox);
        UpdateAiProviderUi();
        SyncAiProviderSelectionToRows();
    }

    private void UpdateComputer2Visibility()
    {
        if (Computer2GroupBox is null || ComputerCountComboBox is null)
        {
            VerboseLogger.Warn("Skipped Computer 2 visibility update because controls are not initialized yet.");
            return;
        }

        Computer2GroupBox.Visibility = ComputerCountComboBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        VerboseLogger.Info($"Computer 2 visibility updated to {Computer2GroupBox.Visibility}.");
    }

    private void UpdateWslCheckboxVisibility()
    {
        if (Computer1WslCheckBox is null || Computer1OsComboBox is null || Computer2WslCheckBox is null || Computer2OsComboBox is null)
        {
            VerboseLogger.Warn("Skipped WSL checkbox visibility update because controls are not initialized yet.");
            return;
        }

        Computer1WslCheckBox.Visibility = Computer1OsComboBox.SelectedItem is OperatingSystemKind.Windows ? Visibility.Visible : Visibility.Collapsed;
        Computer2WslCheckBox.Visibility = Computer2OsComboBox.SelectedItem is OperatingSystemKind.Windows ? Visibility.Visible : Visibility.Collapsed;
        VerboseLogger.Info($"WSL checkbox visibility updated. Computer1={Computer1WslCheckBox.Visibility}, Computer2={Computer2WslCheckBox.Visibility}.");
    }

    private void UpdateAccessModeVisibility()
    {
        UpdateSingleAccessModeVisibility(
            Computer1AccessModeComboBox.SelectedValue is AccessMode accessMode1 ? accessMode1 : AccessMode.DirectLocal,
            Computer1AccessHostLabel,
            Computer1AccessHostTextBox,
            Computer1SshDetailsPanel);

        UpdateSingleAccessModeVisibility(
            Computer2AccessModeComboBox.SelectedValue is AccessMode accessMode2 ? accessMode2 : AccessMode.DirectLocal,
            Computer2AccessHostLabel,
            Computer2AccessHostTextBox,
            Computer2SshDetailsPanel);
    }

    private static void UpdateSingleAccessModeVisibility(
        AccessMode mode,
        UIElement hostLabel,
        UIElement hostTextBox,
        UIElement sshDetailsPanel)
    {
        var needsHost = mode is AccessMode.Tailscale or AccessMode.Ssh;
        hostLabel.Visibility = needsHost ? Visibility.Visible : Visibility.Collapsed;
        hostTextBox.Visibility = needsHost ? Visibility.Visible : Visibility.Collapsed;
        sshDetailsPanel.Visibility = mode == AccessMode.Ssh ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task<List<ValidationRecord>> RunAllValidationsAsync()
    {
        PopulateStateFromControls();
        _state.ValidationResults.Clear();
        _state.ValidationResults.Add(ValidationService.ClassifyTopology(_state));
        _state.ValidationResults.Add(_validationService.ValidateInputConventions(_state));
        _state.ValidationResults.Add(_validationService.ValidateDeploymentModel(_state));
        _state.ValidationResults.Add(_validationService.ValidateConnectivity(_state.Connectivity));
        _state.ValidationResults.Add(await _validationService.ValidateAlpacaPaperAsync(_state.AlpacaPaper));
        _state.ValidationResults.Add(await _validationService.ValidateTelegramAsync(_state.Telegram));
        _state.ValidationResults.Add(await _validationService.ValidateAgentMailAsync(_state.AgentMail));
        _state.ValidationResults.Add(await _validationService.ValidateHermesAiProviderAsync(_state.HermesAiProvider));
        _state.ValidationResults.Add(await _validationService.ValidateAlpacaLiveAsync(_state.AlpacaLive));
        return _state.ValidationResults;
    }

    private void ApproveSummaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_hasPassingFullValidation)
        {
            AppendLog("Approval blocked: validations must pass before the summary can be approved.");
            return;
        }

        RefreshApprovalSummary();
        var confirmation = PromptForApprovalConfirmation();
        if (!string.Equals(confirmation, "APPROVE", StringComparison.Ordinal))
        {
            AppendLog("Summary approval cancelled or rejected. Exact word APPROVE was not entered.");
            return;
        }

        PopulateStateFromControls();
        var summaryText = RenderingService.RenderApprovalSummary(_state);
        var record = ApprovalHistoryStorage.RecordApproval(_state, summaryText);
        _summaryApproved = true;
        UpdateApprovalHistoryDisplay();
        UpdateApprovalAndExportUi();
        AppendLog($"Summary approved and recorded locally at {record.TimestampUtc} UTC.");
    }

    private async void AiProviderTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RunSectionValidationAsync(
            sender as Button,
            AiProviderTestResultTextBlock,
            "Hermes AI Provider",
            () => _validationService.ValidateHermesAiProviderAsync(_state.HermesAiProvider));
    }

    private async void AlpacaPaperTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RunSectionValidationAsync(
            sender as Button,
            AlpacaPaperTestResultTextBlock,
            "Alpaca Paper",
            () => _validationService.ValidateAlpacaPaperAsync(_state.AlpacaPaper));
    }

    private async void TelegramTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RunSectionValidationAsync(
            sender as Button,
            TelegramTestResultTextBlock,
            "Telegram",
            () => _validationService.ValidateTelegramAsync(_state.Telegram));
    }

    private async void AgentMailTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RunSectionValidationAsync(
            sender as Button,
            AgentMailTestResultTextBlock,
            "AgentMail",
            () => _validationService.ValidateAgentMailAsync(_state.AgentMail));
    }

    private async void AlpacaLiveTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RunSectionValidationAsync(
            sender as Button,
            AlpacaLiveTestResultTextBlock,
            "Optional Alpaca Live",
            () => _validationService.ValidateAlpacaLiveAsync(_state.AlpacaLive));
    }

    private void AlpacaLiveEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_uiInitialized)
        {
            return;
        }

        UpdateServiceTestButtonState();
    }

    private async Task RunSectionValidationAsync(
        Button? button,
        TextBlock? resultTextBlock,
        string sectionName,
        Func<Task<ValidationRecord>> validateAsync)
    {
        if (button is null || resultTextBlock is null)
        {
            return;
        }

        PopulateStateFromControls();
        button.IsEnabled = false;
        SetSectionValidationResult(resultTextBlock, sectionName, null, "Running section test...");
        AppendLog($"{sectionName}: running section test...");

        try
        {
            var result = await validateAsync();
            SetSectionValidationResult(resultTextBlock, sectionName, result);
            AppendLog($"{sectionName}: {result.Status} - {result.Message}");
            VerboseLogger.Info($"{sectionName} section test result: {result.Status} :: {result.Message}");
        }
        catch (Exception ex)
        {
            SetSectionValidationResult(resultTextBlock, sectionName, null, $"Latest section test result: FailedBlocking - {ex.Message}");
            AppendLog($"{sectionName}: FailedBlocking - {ex.Message}");
            VerboseLogger.Error($"{sectionName} section test failed.", ex);
            throw;
        }
        finally
        {
            button.IsEnabled = true;
            UpdateServiceTestButtonState();
        }
    }

    private static void SetSectionValidationResult(
        TextBlock textBlock,
        string sectionName,
        ValidationRecord? record,
        string? overrideText = null)
    {
        textBlock.Text = overrideText ?? $"Latest section test result for {sectionName}: {record!.Status} - {record.Message}";
        textBlock.Foreground = record?.Status switch
        {
            ValidationStatus.Passed => Brushes.DarkGreen,
            ValidationStatus.PassedWithWarning => Brushes.DarkGoldenrod,
            ValidationStatus.Skipped => Brushes.DimGray,
            ValidationStatus.FailedBlocking => Brushes.DarkRed,
            _ => Brushes.DimGray,
        };
    }

    private void UpdateServiceTestButtonState()
    {
        if (AlpacaLiveEnabledCheckBox is null || AlpacaLiveTestButton is null || AlpacaLiveTestResultTextBlock is null)
        {
            return;
        }

        var liveEnabled = AlpacaLiveEnabledCheckBox.IsChecked == true;
        AlpacaLiveTestButton.IsEnabled = liveEnabled;
        if (!liveEnabled)
        {
            SetSectionValidationResult(
                AlpacaLiveTestResultTextBlock,
                "Optional Alpaca Live",
                null,
                "Latest section test result for Optional Alpaca Live: Skipped - Enable optional live credentials to test this section.");
        }
    }

    private void AnyWizardTextChanged(object sender, TextChangedEventArgs e)
    {
        HandleWizardInputChanged(e.OriginalSource as DependencyObject);
    }

    private void AnyWizardSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        HandleWizardInputChanged(e.OriginalSource as DependencyObject);
    }

    private void AnyWizardToggleChanged(object sender, RoutedEventArgs e)
    {
        HandleWizardInputChanged(e.OriginalSource as DependencyObject);
    }

    private void HandleWizardInputChanged(DependencyObject? source)
    {
        if (!_uiInitialized || _isLoadingState)
        {
            return;
        }

        if (source is null ||
            IsWithinElement(source, LogTextBox) ||
            IsWithinElement(source, ApprovalSummaryTextBox) ||
            IsWithinElement(source, ApprovalHistoryTextBox) ||
            IsWithinElement(source, WizardTabs))
        {
            return;
        }

        MarkValidationStateDirty();
    }

    private void SyncAiProviderSelectionFromRows()
    {
        var backendRow = _targetRows.FirstOrDefault(row => row.IsAuthoritativeBackend || row.HermesBackend);
        if (backendRow is not null && AiProviderComboBox is not null && AiProviderServicesComboBox is not null)
        {
            var provider = HermesProviderCatalog.Find(string.IsNullOrWhiteSpace(backendRow.AiProviderKey)
                ? _state.HermesAiProvider.ProviderKey
                : backendRow.AiProviderKey);
            AiProviderComboBox.SelectedItem = provider;
            AiProviderServicesComboBox.SelectedItem = provider;
        }
    }

    private void SyncAiProviderSelectionToRows()
    {
        var providerKey = GetSelectedProviderKey();
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return;
        }

        foreach (var row in _targetRows)
        {
            row.AiProviderKey = row.HermesBackend || row.IsAuthoritativeBackend ? providerKey : string.Empty;
        }
    }

    private void UpdateAiProviderUi()
    {
        if (AiProviderComboBox is null ||
            AiProviderServicesComboBox is null ||
            AiProviderBaseUrlLabel is null ||
            AiProviderModelLabel is null ||
            AiProviderApiKeyLabel is null ||
            AiProviderHelperTextBlock is null)
        {
            return;
        }

        var provider = GetSelectedProviderOption();
        AiProviderGroupBox.Header = $"{provider.DisplayName} Configuration";
        AiProviderBaseUrlLabel.Text = provider.BaseUrlLabel;
        AiProviderModelLabel.Text = provider.ModelLabel;
        AiProviderApiKeyLabel.Text = provider.ApiKeyLabel;
        AiProviderHelperTextBlock.Text = provider.HelperText;
        AiProviderTargetsTextBlock.Text = $"Selected backend provider: {provider.DisplayName}";

        if (ShouldReplaceProviderBaseUrl(AiProviderBaseUrlTextBox.Text))
        {
            AiProviderBaseUrlTextBox.Text = provider.DefaultBaseUrl;
        }
    }

    private void SyncProviderComboSelections(ComboBox source)
    {
        if (source.SelectedItem is not HermesProviderOption provider)
        {
            return;
        }

        if (!Equals(AiProviderComboBox.SelectedItem, provider))
        {
            AiProviderComboBox.SelectedItem = provider;
        }

        if (!Equals(AiProviderServicesComboBox.SelectedItem, provider))
        {
            AiProviderServicesComboBox.SelectedItem = provider;
        }
    }

    private static bool ShouldReplaceProviderBaseUrl(string currentValue)
    {
        var trimmed = currentValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        return HermesProviderCatalog.All
            .Select(provider => provider.DefaultBaseUrl)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(defaultValue => string.Equals(trimmed, defaultValue, StringComparison.OrdinalIgnoreCase));
    }

    private HermesProviderOption GetSelectedProviderOption()
    {
        if (AiProviderComboBox?.SelectedItem is HermesProviderOption provider)
        {
            return provider;
        }

        return HermesProviderCatalog.Find(_state.HermesAiProvider.ProviderKey);
    }

    private string GetSelectedProviderKey() => GetSelectedProviderOption().Key;

    private void CashAllocationMode_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingCashInputs)
        {
            return;
        }

        if (!_uiInitialized)
        {
            return;
        }

        VerboseLogger.Info("Cash allocation mode changed.");
        UpdateCashAllocationHelperText();
    }

    private void CashAllocationField_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizingCashInputs || sender is not TextBox textBox)
        {
            return;
        }

        if (!_uiInitialized)
        {
            return;
        }

        var allowDecimal = textBox != MaxRankedCandidatesTextBox;
        var sanitized = CashAllocationCalculator.SanitizeNumericInput(textBox.Text, allowDecimal);
        if (!string.Equals(textBox.Text, sanitized, StringComparison.Ordinal))
        {
            _isSynchronizingCashInputs = true;
            var caretIndex = textBox.CaretIndex;
            textBox.Text = sanitized;
            textBox.CaretIndex = Math.Min(caretIndex, textBox.Text.Length);
            _isSynchronizingCashInputs = false;
        }

        UpdateCashAllocationHelperText();
    }

    private void CashAllocationField_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_uiInitialized)
        {
            return;
        }

        NormalizeCashAllocationInputs();
        UpdateCashAllocationHelperText();
    }

    private void DecimalField_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsValidNextInput(sender as TextBox, e.Text, allowDecimal: true);
    }

    private void IntegerField_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsValidNextInput(sender as TextBox, e.Text, allowDecimal: false);
    }

    private void DecimalField_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        HandlePaste(sender as TextBox, e, allowDecimal: true);
    }

    private void IntegerField_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        HandlePaste(sender as TextBox, e, allowDecimal: false);
    }

    private void NormalizeCashAllocationInputs()
    {
        _isSynchronizingCashInputs = true;
        CashBasisTextBox.Text = NormalizeDecimalText(CashBasisTextBox.Text, 25000m);
        ProtectedReserveValueTextBox.Text = NormalizeDecimalText(ProtectedReserveValueTextBox.Text, 10m);
        PerTickerValueTextBox.Text = NormalizeDecimalText(PerTickerValueTextBox.Text, 10m);
        MaxRankedCandidatesTextBox.Text = NormalizeIntegerText(MaxRankedCandidatesTextBox.Text, 5);
        _isSynchronizingCashInputs = false;
    }

    private void UpdateCashAllocationHelperText()
    {
        if (CashBasisTextBox is null ||
            ProtectedReservePercentRadio is null ||
            ProtectedReserveValueTextBox is null ||
            PerTickerPercentRadio is null ||
            PerTickerValueTextBox is null ||
            ProtectedReserveHelperTextBlock is null ||
            PerTickerHelperTextBlock is null)
        {
            VerboseLogger.Warn("Skipped cash allocation helper update because controls are not initialized yet.");
            return;
        }

        var basis = ParseDecimalOrDefault(CashBasisTextBox.Text, 0m);
        var protectedReserve = BuildAllocationInput(ProtectedReservePercentRadio, ProtectedReserveValueTextBox, 10m);
        var perTicker = BuildAllocationInput(PerTickerPercentRadio, PerTickerValueTextBox, 10m);

        ProtectedReserveHelperTextBlock.Text = BuildAllocationHelperText(protectedReserve, basis);
        PerTickerHelperTextBlock.Text = BuildAllocationHelperText(perTicker, basis);
    }

    private static string BuildAllocationHelperText(AllocationInput input, decimal basis)
    {
        if (basis <= 0m)
        {
            return input.Mode == AllocationEntryMode.Percent
                ? "Enter a start-of-day cash basis to preview the dollar value."
                : "Enter a start-of-day cash basis to preview the effective percent.";
        }

        var dollarValue = CashAllocationCalculator.CalculateDollarValue(input, basis);
        var percentValue = CashAllocationCalculator.CalculatePercentValue(input, basis);
        return input.Mode == AllocationEntryMode.Percent
            ? $"= ${dollarValue:0,0.00} based on ${basis:0,0.00}"
            : $"= {percentValue:0.####}% based on ${basis:0,0.00}";
    }

    private static string NormalizeDecimalText(string input, decimal fallback)
    {
        var sanitized = CashAllocationCalculator.SanitizeNumericInput(input, allowDecimal: true);
        return decimal.TryParse(sanitized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value.ToString("0.##", CultureInfo.InvariantCulture)
            : fallback.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string NormalizeIntegerText(string input, int fallback)
    {
        var sanitized = CashAllocationCalculator.SanitizeNumericInput(input, allowDecimal: false);
        return int.TryParse(sanitized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value.ToString(CultureInfo.InvariantCulture)
            : fallback.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsValidNextInput(TextBox? textBox, string newText, bool allowDecimal)
    {
        if (textBox is null)
        {
            return false;
        }

        var proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, newText);
        var sanitized = CashAllocationCalculator.SanitizeNumericInput(proposed, allowDecimal);
        return string.Equals(proposed, sanitized, StringComparison.Ordinal);
    }

    private static void HandlePaste(TextBox? textBox, DataObjectPastingEventArgs e, bool allowDecimal)
    {
        if (textBox is null)
        {
            e.CancelCommand();
            return;
        }

        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pasteText = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!IsValidNextInput(textBox, pasteText, allowDecimal))
        {
            e.CancelCommand();
        }
    }

    private static decimal ParseDecimalOrDefault(string input, decimal defaultValue)
    {
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static int ParseIntOrDefault(string input, int defaultValue)
    {
        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : defaultValue;
    }

    private void LoadServicePlacementRows()
    {
        LoadServicePlacementRows(_computer1ServiceRows, _state.Computers.ElementAtOrDefault(0));
        LoadServicePlacementRows(_computer2ServiceRows, _state.Computers.ElementAtOrDefault(1));
    }

    private static void LoadServicePlacementRows(ObservableCollection<ComputerServicePlacementRow> rows, ComputerDefinition? computer)
    {
        rows.Clear();
        var placements = computer?.ServicePlacements ?? [];
        foreach (var service in DeploymentCatalog.SupportedServices)
        {
            var placement = placements.FirstOrDefault(item => item.Service == service)?.PlacementMode
                ?? ServicePlacementMode.NotOnThisComputer;
            rows.Add(new ComputerServicePlacementRow
            {
                ServiceKind = service,
                DisplayName = DeploymentCatalog.GetServiceDisplayName(service),
                PlacementMode = placement,
            });
        }
    }

    private static List<ServicePlacement> BuildServicePlacements(IEnumerable<ComputerServicePlacementRow> rows) =>
        rows.Select(row => new ServicePlacement
        {
            Service = row.ServiceKind,
            PlacementMode = row.PlacementMode,
        }).ToList();

    private void RefreshDeploymentModelDefaults()
    {
        if (RequiresTailscaleCheckBox is null ||
            ComputerCountComboBox is null ||
            Computer1PlacementGroupBox is null ||
            Computer2PlacementGroupBox is null ||
            Computer1LabelTextBox is null ||
            Computer2LabelTextBox is null)
        {
            VerboseLogger.Warn("Skipped deployment model refresh because placement controls are not initialized yet.");
            return;
        }

        var draftState = new WizardState
        {
            Computers = BuildComputersFromControls(),
            Targets = BuildTargetsFromRows(),
            Connectivity = new ConnectivityConfiguration
            {
                RequiresTailscale = RequiresTailscaleCheckBox.IsChecked == true,
            }
        };

        TopologyService.ApplyDefaultDeploymentModel(draftState);
        _state.Computers = draftState.Computers;
        LoadServicePlacementRows();
        UpdateServicePlacementVisibility();
    }

    private void UpdateServicePlacementVisibility()
    {
        if (Computer1LabelTextBox is null ||
            Computer2LabelTextBox is null ||
            Computer1PlacementGroupBox is null ||
            Computer2PlacementGroupBox is null ||
            ComputerCountComboBox is null)
        {
            VerboseLogger.Warn("Skipped service placement visibility update because placement controls are not initialized yet.");
            return;
        }

        var computer1Label = string.IsNullOrWhiteSpace(Computer1LabelTextBox.Text) ? "Computer 1" : Computer1LabelTextBox.Text.Trim();
        var computer2Label = string.IsNullOrWhiteSpace(Computer2LabelTextBox.Text) ? "Computer 2" : Computer2LabelTextBox.Text.Trim();
        Computer1PlacementGroupBox.Header = $"{computer1Label} Service Placement";
        Computer2PlacementGroupBox.Visibility = ComputerCountComboBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        Computer2PlacementGroupBox.Header = $"{computer2Label} Service Placement";
    }

    private void AppendLog(string message)
    {
        if (LogTextBox is null)
        {
            return;
        }

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

    private static bool IsWithinElement(DependencyObject source, DependencyObject? ancestor)
    {
        if (ancestor is null)
        {
            return false;
        }

        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void MarkValidationStateDirty(bool logChange = true)
    {
        _hasPassingFullValidation = false;
        _summaryApproved = false;
        UpdateApprovalAndExportUi();
        if (logChange)
        {
            VerboseLogger.Info("Wizard inputs changed. Validation and approval state reset to pending.");
        }
    }

    private void UpdateApprovalAndExportUi()
    {
        if (ApprovalTab is null || ApproveSummaryButton is null || ExportButton is null)
        {
            return;
        }

        ApprovalTab.IsEnabled = _hasPassingFullValidation;
        ApproveSummaryButton.IsEnabled = _hasPassingFullValidation;
        ExportButton.IsEnabled = _hasPassingFullValidation && _summaryApproved;
        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        if (BackButton is null || NextButton is null || WizardTabs is null)
        {
            return;
        }

        BackButton.IsEnabled = WizardTabs.SelectedIndex > 0;
        NextButton.IsEnabled = WizardTabs.SelectedIndex < WizardTabs.Items.Count - 1
            && (WizardTabs.SelectedIndex != GetReviewTabIndex() || _hasPassingFullValidation);
    }

    private void RefreshApprovalSummary()
    {
        if (ApprovalSummaryTextBox is null)
        {
            return;
        }

        PopulateStateFromControls();
        ApprovalSummaryTextBox.Text = RenderingService.RenderApprovalSummary(_state);
    }

    private void UpdateApprovalHistoryDisplay()
    {
        if (ApprovalHistoryTextBox is null)
        {
            return;
        }

        ApprovalHistoryTextBox.Text = ApprovalHistoryStorage.LoadHistoryDisplayText();
    }

    private string? PromptForApprovalConfirmation()
    {
        var dialog = new Window
        {
            Title = "Confirm Summary Approval",
            Width = 420,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = this,
        };

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var prompt = new TextBlock
        {
            Text = "Type APPROVE exactly to document approval for this redacted configuration summary.",
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(prompt, 0);
        root.Children.Add(prompt);

        var input = new TextBox { Margin = new Thickness(0, 12, 0, 12) };
        Grid.SetRow(input, 1);
        root.Children.Add(input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var okButton = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", Width = 90, IsCancel = true };
        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        dialog.Content = root;
        var result = dialog.ShowDialog();
        return result == true ? input.Text.Trim() : null;
    }

    private int GetReviewTabIndex() => Math.Max(0, WizardTabs.Items.Count - 2);
}
