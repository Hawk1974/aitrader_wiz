using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TargetAssignmentRow> _targetRows = [];
    private readonly ValidationService _validationService = new(new HttpClient());
    private WizardState _state = WizardStateFactory.CreateDefault();
    private bool _isSynchronizingCashInputs;
    private bool _uiInitialized;

    public MainWindow()
    {
        VerboseLogger.Info("MainWindow constructor entered.");
        InitializeComponent();
        InitializeWindowIcon();
        TargetsDataGrid.ItemsSource = _targetRows;
        InitializeOsSelectors();
        LoadStateIntoControls();
        _uiInitialized = true;
        AppendLog($"Verbose log file: {VerboseLogger.CurrentLogPath}");
        VerboseLogger.Info("MainWindow constructor completed.");
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
    }

    private void LoadStateIntoControls()
    {
        VerboseLogger.Info("Loading wizard state into UI controls.");
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

        LmStudioBaseUrlTextBox.Text = _state.LmStudio.BaseUrl;
        LmStudioModelIdTextBox.Text = _state.LmStudio.ModelId;
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
        UpdateComputer2Visibility();
        UpdateWslCheckboxVisibility();
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

        _state.LmStudio.BaseUrl = LmStudioBaseUrlTextBox.Text.Trim();
        _state.LmStudio.ModelId = LmStudioModelIdTextBox.Text.Trim();
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
        VerboseLogger.Info($"Runtime target rows rebuilt. Count={_targetRows.Count}.");
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

    private async void RunValidationsButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Run Validations button clicked.");
        try
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
            AppendLog("Encrypted local draft saved.");
            VerboseLogger.Info("Encrypted local draft saved successfully.");
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
            AppendLog("Encrypted local draft loaded.");
            VerboseLogger.Info("Encrypted local draft loaded successfully.");
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
            AppendLog("Local draft wiped.");
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
            PopulateStateFromControls();
            var topologyErrors = TopologyService.ValidateTopology(_state);
            if (topologyErrors.Count > 0)
            {
                AppendLog("Export blocked:");
                foreach (var error in topologyErrors)
                {
                    AppendLog($"- {error}");
                    VerboseLogger.Warn($"Export blocked by topology error: {error}");
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
                VerboseLogger.Info("Export dialog cancelled by user.");
                return;
            }

            ExportService.ExportOverlayZip(_state, dialog.FileName);
            AppendLog($"Overlay files exported to: {dialog.FileName}");
            VerboseLogger.Info($"Overlay zip exported successfully to {dialog.FileName}.");
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("Overlay export failed.", ex);
            AppendLog($"Export failed: {ex.Message}");
            throw;
        }
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

        VerboseLogger.Warn("Application exit confirmed before overlay export.");
        AppendLog("Exiting without exporting overlay files.");
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

    private void DeriveTargetsButton_OnClick(object sender, RoutedEventArgs e)
    {
        VerboseLogger.Info("Derive Targets button clicked.");
        RebuildTargetRows();
        AppendLog("Runtime targets re-derived from the current computer selections.");
        WizardTabs.SelectedIndex = 2;
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
}
