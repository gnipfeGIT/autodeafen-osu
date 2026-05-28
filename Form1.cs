namespace AutoDeafenOsu;

public partial class Form1 : Form
{
    private readonly AppSettings _settings;
    private readonly OsuTelemetryClient _osuClient = new();
    private readonly DiscordHotkeySender _hotkeySender = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly NotifyIcon _notifyIcon;

    private readonly TextBox _endpointTextBox = new();
    private readonly TextBox _hotkeyTextBox = new();
    private readonly ComboBox _hotkeyMethodComboBox = new();
    private readonly NumericUpDown _pollIntervalInput = new();
    private readonly NumericUpDown _minComboInput = new();
    private readonly CheckBox _useMapPercentCheckBox = new();
    private readonly NumericUpDown _maxComboPercentInput = new();
    private readonly CheckBox _undeafenOnMissCheckBox = new();
    private readonly CheckBox _undeafenWhenNotPlayingCheckBox = new();
    private readonly CheckBox _startMonitoringOnLaunchCheckBox = new();
    private readonly CheckBox _minimizeToTrayCheckBox = new();
    private readonly CheckBox _discordCurrentlyDeafenedCheckBox = new();
    private readonly Button _monitorButton = new();
    private readonly Button _testHotkeyButton = new();
    private readonly Button _saveButton = new();
    private readonly Label _statusValueLabel = new();
    private readonly Label _beatmapValueLabel = new();
    private readonly Label _comboValueLabel = new();
    private readonly Label _missValueLabel = new();
    private readonly Label _maxComboValueLabel = new();
    private readonly Label _thresholdValueLabel = new();
    private readonly Label _decisionValueLabel = new();
    private readonly Label _deafenValueLabel = new();
    private readonly Label _lastActionValueLabel = new();

    private bool _monitoring;
    private bool _polling;
    private bool _discordDeafened;
    private bool _wasPlaying;
    private bool _fcBrokenThisRun;
    private bool _autoDeafenSentThisRun;
    private int _lastCombo;
    private int _lastScore;
    private int _lastMapTimeMs;

    public Form1()
    {
        _settings = AppSettings.Load();
        InitializeComponent();
        Text = "Auto Deafen osu!";
        MinimumSize = new Size(840, 620);
        Size = new Size(940, 680);
        StartPosition = FormStartPosition.CenterScreen;

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Auto Deafen osu!",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();

        BuildUi();
        LoadSettingsIntoUi();

        _timer.Tick += OnTimerTick;
        ApplyTimerInterval();

        if (_settings.StartMonitoringOnLaunch)
        {
            StartMonitoring();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _minimizeToTrayCheckBox.Checked)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(1200, "Auto Deafen osu!", "Still monitoring from the tray.", ToolTipIcon.Info);
            return;
        }

        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _osuClient.Dispose();
        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
            BackColor = Color.FromArgb(248, 249, 251)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "Auto Deafen osu!",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(26, 32, 44),
            Margin = new Padding(0, 0, 0, 4)
        };
        root.Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Deafens Discord through your configured Discord Toggle Deafen keybind while osu! is still on full-combo pace.",
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = Color.FromArgb(74, 85, 104),
            Margin = new Padding(0, 0, 0, 16)
        };
        root.Controls.Add(subtitle);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        root.Controls.Add(content);

        content.Controls.Add(BuildSettingsPanel(), 0, 0);
        content.Controls.Add(BuildStatusPanel(), 1, 0);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 16, 0, 0)
        };
        root.Controls.Add(actions);

        ConfigureButton(_monitorButton, "Start Monitoring", Color.FromArgb(47, 133, 90));
        _monitorButton.Click += (_, _) =>
        {
            if (_monitoring)
            {
                StopMonitoring("Stopped by user.");
            }
            else
            {
                SaveSettingsFromUi();
                StartMonitoring();
            }
        };
        actions.Controls.Add(_monitorButton);

        ConfigureButton(_testHotkeyButton, "Test Deafen Hotkey", Color.FromArgb(49, 130, 206));
        _testHotkeyButton.Click += (_, _) =>
        {
            SaveSettingsFromUi();
            SendDiscordHotkey("Manual hotkey test sent. Sync the checkbox if Discord changed.");
        };
        actions.Controls.Add(_testHotkeyButton);

        ConfigureButton(_saveButton, "Save Settings", Color.FromArgb(74, 85, 104));
        _saveButton.Click += (_, _) =>
        {
            SaveSettingsFromUi();
            SetLastAction($"Settings saved to {AppSettings.SettingsPath}");
        };
        actions.Controls.Add(_saveButton);
    }

    private Control BuildSettingsPanel()
    {
        var panel = BuildPanel("Settings");
        var grid = GetPanelGrid(panel);

        AddTextRow(grid, "tosu/gosumemory URL", _endpointTextBox);
        AddTextRow(grid, "Discord hotkey", _hotkeyTextBox);
        AddComboRow(grid, "Hotkey method", _hotkeyMethodComboBox);
        AddNumberRow(grid, "Poll interval", _pollIntervalInput, 150, 5000, 50, "ms");
        AddCheckRow(grid, _useMapPercentCheckBox, "Use map max combo percentage");
        AddNumberRow(grid, "Deafen at", _maxComboPercentInput, 1, 100, 1, "percent");
        AddNumberRow(grid, "Fallback combo", _minComboInput, 0, 9999, 1, "combo");
        AddCheckRow(grid, _undeafenOnMissCheckBox, "Undeafen after a miss or combo break");
        AddCheckRow(grid, _undeafenWhenNotPlayingCheckBox, "Undeafen when gameplay stops");
        AddCheckRow(grid, _startMonitoringOnLaunchCheckBox, "Start monitoring when app opens");
        AddCheckRow(grid, _minimizeToTrayCheckBox, "Minimize to tray when closed");
        AddCheckRow(grid, _discordCurrentlyDeafenedCheckBox, "Discord deafened already?");
        _discordCurrentlyDeafenedCheckBox.CheckedChanged += (_, _) =>
        {
            _discordDeafened = _discordCurrentlyDeafenedCheckBox.Checked;
            RefreshDeafenLabel();
        };

        return panel;
    }

    private Control BuildStatusPanel()
    {
        var panel = BuildPanel("Live Status");
        var grid = GetPanelGrid(panel);

        AddValueRow(grid, "Monitor", _statusValueLabel);
        AddValueRow(grid, "Beatmap", _beatmapValueLabel);
        AddValueRow(grid, "Combo", _comboValueLabel);
        AddValueRow(grid, "Misses", _missValueLabel);
        AddValueRow(grid, "Max combo", _maxComboValueLabel);
        AddValueRow(grid, "Deafen at", _thresholdValueLabel);
        AddValueRow(grid, "Decision", _decisionValueLabel);
        AddValueRow(grid, "Discord deafen", _deafenValueLabel);
        AddValueRow(grid, "Last action", _lastActionValueLabel);

        _statusValueLabel.Text = "Idle";
        _beatmapValueLabel.Text = "Waiting for telemetry";
        _comboValueLabel.Text = "0";
        _missValueLabel.Text = "0";
        _maxComboValueLabel.Text = "Unknown";
        _thresholdValueLabel.Text = "Waiting for telemetry";
        _decisionValueLabel.Text = "Idle";
        _lastActionValueLabel.Text = "Open osu!, run tosu, then start monitoring.";
        RefreshDeafenLabel();

        return panel;
    }

    private static TableLayoutPanel GetPanelGrid(Panel panel)
    {
        var layout = (TableLayoutPanel)panel.Controls[0];
        return (TableLayoutPanel)layout.Controls[1];
    }

    private static Panel BuildPanel(string heading)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            BackColor = Color.White,
            Margin = new Padding(0, 0, 12, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = heading,
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(26, 32, 44),
            Margin = new Padding(0, 0, 0, 12)
        });

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(grid);

        return panel;
    }

    private static void AddTextRow(TableLayoutPanel grid, string labelText, TextBox textBox)
    {
        textBox.Dock = DockStyle.Top;
        textBox.Margin = new Padding(0, 0, 0, 10);
        AddLabel(grid, labelText);
        grid.Controls.Add(textBox);
    }

    private static void AddComboRow(TableLayoutPanel grid, string labelText, ComboBox comboBox)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Dock = DockStyle.Top;
        comboBox.Margin = new Padding(0, 0, 0, 10);
        AddLabel(grid, labelText);
        grid.Controls.Add(comboBox);
    }

    private static void AddNumberRow(TableLayoutPanel grid, string labelText, NumericUpDown input, int min, int max, int increment, string suffix)
    {
        input.Minimum = min;
        input.Maximum = max;
        input.Increment = increment;
        input.Dock = DockStyle.Left;
        input.Width = 90;

        var holder = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        holder.Controls.Add(input);
        holder.Controls.Add(new Label
        {
            Text = suffix,
            AutoSize = true,
            Margin = new Padding(8, 5, 0, 0),
            ForeColor = Color.FromArgb(74, 85, 104)
        });

        AddLabel(grid, labelText);
        grid.Controls.Add(holder);
    }

    private static void AddCheckRow(TableLayoutPanel grid, CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.Margin = new Padding(0, 4, 0, 10);

        grid.Controls.Add(checkBox);
        grid.SetColumnSpan(checkBox, 2);
    }

    private static void AddValueRow(TableLayoutPanel grid, string labelText, Label valueLabel)
    {
        valueLabel.AutoSize = true;
        valueLabel.MaximumSize = new Size(250, 0);
        valueLabel.Margin = new Padding(0, 0, 0, 12);
        valueLabel.ForeColor = Color.FromArgb(26, 32, 44);

        AddLabel(grid, labelText);
        grid.Controls.Add(valueLabel);
    }

    private static void AddLabel(TableLayoutPanel grid, string text)
    {
        grid.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 3, 12, 10),
            ForeColor = Color.FromArgb(74, 85, 104)
        });
    }

    private static void ConfigureButton(Button button, string text, Color backColor)
    {
        button.Text = text;
        button.AutoSize = true;
        button.MinimumSize = new Size(138, 36);
        button.BackColor = backColor;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = new Padding(0, 0, 10, 0);
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Start Monitoring", null, (_, _) =>
        {
            SaveSettingsFromUi();
            StartMonitoring();
        });
        menu.Items.Add("Stop Monitoring", null, (_, _) => StopMonitoring("Stopped from tray."));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _minimizeToTrayCheckBox.Checked = false;
            Close();
        });
        return menu;
    }

    private void LoadSettingsIntoUi()
    {
        _hotkeyMethodComboBox.Items.Clear();
        _hotkeyMethodComboBox.Items.AddRange(Enum.GetValues<HotkeySendMethod>().Cast<object>().ToArray());
        _hotkeyMethodComboBox.SelectedItem = _settings.HotkeySendMethod;
        if (_hotkeyMethodComboBox.SelectedIndex < 0)
        {
            _hotkeyMethodComboBox.SelectedItem = HotkeySendMethod.SendInputScanCode;
        }

        _endpointTextBox.Text = _settings.GosumemoryEndpoint;
        _hotkeyTextBox.Text = _settings.DiscordToggleDeafenHotkey;
        _pollIntervalInput.Value = Math.Clamp(_settings.PollIntervalMs, (int)_pollIntervalInput.Minimum, (int)_pollIntervalInput.Maximum);
        _minComboInput.Value = Math.Clamp(_settings.MinComboToDeafen, (int)_minComboInput.Minimum, (int)_minComboInput.Maximum);
        _useMapPercentCheckBox.Checked = _settings.UseMapMaxComboPercent;
        _maxComboPercentInput.Value = Math.Clamp(_settings.MaxComboPercentToDeafen, (int)_maxComboPercentInput.Minimum, (int)_maxComboPercentInput.Maximum);
        _undeafenOnMissCheckBox.Checked = _settings.UndeafenOnMiss;
        _undeafenWhenNotPlayingCheckBox.Checked = _settings.UndeafenWhenNotPlaying;
        _startMonitoringOnLaunchCheckBox.Checked = _settings.StartMonitoringOnLaunch;
        _minimizeToTrayCheckBox.Checked = _settings.MinimizeToTray;
    }

    private void SaveSettingsFromUi()
    {
        _settings.GosumemoryEndpoint = _endpointTextBox.Text.Trim();
        _settings.DiscordToggleDeafenHotkey = _hotkeyTextBox.Text.Trim();
        _settings.HotkeySendMethod = _hotkeyMethodComboBox.SelectedItem is HotkeySendMethod method
            ? method
            : HotkeySendMethod.SendInputScanCode;
        _settings.PollIntervalMs = (int)_pollIntervalInput.Value;
        _settings.MinComboToDeafen = (int)_minComboInput.Value;
        _settings.UseMapMaxComboPercent = _useMapPercentCheckBox.Checked;
        _settings.MaxComboPercentToDeafen = (int)_maxComboPercentInput.Value;
        _settings.UndeafenOnMiss = _undeafenOnMissCheckBox.Checked;
        _settings.UndeafenWhenNotPlaying = _undeafenWhenNotPlayingCheckBox.Checked;
        _settings.StartMonitoringOnLaunch = _startMonitoringOnLaunchCheckBox.Checked;
        _settings.MinimizeToTray = _minimizeToTrayCheckBox.Checked;
        _settings.Save();
        ApplyTimerInterval();
    }

    private void ApplyTimerInterval()
    {
        _timer.Interval = Math.Clamp((int)_pollIntervalInput.Value, 150, 5000);
    }

    private void StartMonitoring()
    {
        _monitoring = true;
        _discordDeafened = _discordCurrentlyDeafenedCheckBox.Checked;
        _wasPlaying = false;
        _fcBrokenThisRun = false;
        _autoDeafenSentThisRun = false;
        _lastCombo = 0;
        _lastScore = 0;
        _lastMapTimeMs = 0;
        _monitorButton.Text = "Stop Monitoring";
        _statusValueLabel.Text = "Monitoring";
        ApplyTimerInterval();
        _timer.Start();
        SetLastAction("Monitoring started.");
    }

    private void StopMonitoring(string reason)
    {
        _monitoring = false;
        _timer.Stop();
        _monitorButton.Text = "Start Monitoring";
        _statusValueLabel.Text = "Idle";
        SetLastAction(reason);
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_polling)
        {
            return;
        }

        _polling = true;
        try
        {
            var snapshot = await _osuClient.ReadAsync(_settings.GosumemoryEndpoint, CancellationToken.None);
            UpdateSnapshotUi(snapshot);
            ReactToSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            _statusValueLabel.Text = "Telemetry unavailable";
            SetLastAction(FormatTelemetryError(ex));
        }
        finally
        {
            _polling = false;
        }
    }

    private void UpdateSnapshotUi(OsuTelemetrySnapshot snapshot)
    {
        _statusValueLabel.Text = snapshot.IsPlaying ? "Playing" : "Connected";
        _beatmapValueLabel.Text = snapshot.BeatmapTitle;
        _comboValueLabel.Text = snapshot.Combo.ToString();
        _missValueLabel.Text = snapshot.Misses.ToString();
        _maxComboValueLabel.Text = snapshot.MaxCombo?.ToString() ?? "Unknown";
        _thresholdValueLabel.Text = $"{GetDeafenThreshold(snapshot)} combo";
    }

    private void ReactToSnapshot(OsuTelemetrySnapshot snapshot)
    {
        if (!snapshot.IsPlaying)
        {
            if (_wasPlaying && _settings.UndeafenWhenNotPlaying && _discordDeafened)
            {
                ToggleDiscordDeafen("Gameplay stopped; undeafened Discord.");
            }

            _wasPlaying = false;
            _fcBrokenThisRun = false;
            _autoDeafenSentThisRun = false;
            _lastCombo = 0;
            _lastScore = 0;
            _lastMapTimeMs = 0;
            _decisionValueLabel.Text = "Waiting for gameplay";
            return;
        }

        var newRunStarted = IsNewRun(snapshot);
        if (!_wasPlaying || newRunStarted)
        {
            _fcBrokenThisRun = false;
            _autoDeafenSentThisRun = false;
            _lastCombo = 0;
            _lastScore = 0;
            _lastMapTimeMs = snapshot.MapTimeMs;
        }

        var comboDropped = _wasPlaying
            && !newRunStarted
            && snapshot.Misses == 0
            && _lastCombo > 0
            && snapshot.Combo + 2 < _lastCombo;

        _wasPlaying = true;
        RememberSnapshot(snapshot);

        if (snapshot.Misses > 0 || comboDropped)
        {
            _fcBrokenThisRun = true;
            if (_settings.UndeafenOnMiss && _discordDeafened)
            {
                ToggleDiscordDeafen(comboDropped
                    ? "Combo dropped; undeafened Discord."
                    : "FC broken; undeafened Discord.");
            }

            _decisionValueLabel.Text = comboDropped ? "FC broken: combo dropped" : "FC broken: miss detected";
            return;
        }

        var deafenThreshold = GetDeafenThreshold(snapshot);
        if (_fcBrokenThisRun)
        {
            _decisionValueLabel.Text = "Waiting for next run: FC already broken";
            return;
        }

        if (_autoDeafenSentThisRun)
        {
            _decisionValueLabel.Text = "Auto-deafen already sent this run";
            return;
        }

        if (snapshot.Combo < deafenThreshold)
        {
            _decisionValueLabel.Text = $"Waiting for {deafenThreshold - snapshot.Combo} more combo";
            return;
        }

        if (_discordDeafened)
        {
            _decisionValueLabel.Text = "Threshold reached, but marked already deafened";
            return;
        }

        if (snapshot.Combo >= deafenThreshold)
        {
            _autoDeafenSentThisRun = true;
            ToggleDiscordDeafen($"Still FCing at {snapshot.Combo}/{snapshot.MaxCombo?.ToString() ?? "?"} combo; deafened Discord.");
        }
    }

    private int GetDeafenThreshold(OsuTelemetrySnapshot snapshot)
    {
        if (_settings.UseMapMaxComboPercent && snapshot.MaxCombo is > 0)
        {
            return Math.Max(1, (int)Math.Ceiling(snapshot.MaxCombo.Value * _settings.MaxComboPercentToDeafen / 100.0));
        }

        return _settings.MinComboToDeafen;
    }

    private bool IsNewRun(OsuTelemetrySnapshot snapshot)
    {
        if (!_wasPlaying)
        {
            return true;
        }

        if (_lastMapTimeMs > 3000 && snapshot.MapTimeMs + 1500 < _lastMapTimeMs)
        {
            return true;
        }

        if (_lastScore > 0 && snapshot.Score < _lastScore)
        {
            return true;
        }

        return _lastCombo > 20 && snapshot.Combo <= 1 && snapshot.Misses == 0 && snapshot.Score <= 1000;
    }

    private void RememberSnapshot(OsuTelemetrySnapshot snapshot)
    {
        _lastCombo = snapshot.Combo;
        _lastScore = snapshot.Score;
        _lastMapTimeMs = snapshot.MapTimeMs;
    }

    private void ToggleDiscordDeafen(string reason)
    {
        if (SendDiscordHotkey(reason))
        {
            _discordDeafened = !_discordDeafened;
            _discordCurrentlyDeafenedCheckBox.Checked = _discordDeafened;
            RefreshDeafenLabel();
        }
    }

    private bool SendDiscordHotkey(string reason)
    {
        try
        {
            _hotkeySender.SendToggleDeafen(_settings.DiscordToggleDeafenHotkey, _settings.HotkeySendMethod);
            SetLastAction(reason);
            return true;
        }
        catch (Exception ex)
        {
            SetLastAction($"Hotkey failed: {ex.Message}");
            return false;
        }
    }

    private void RefreshDeafenLabel()
    {
        _deafenValueLabel.Text = _discordDeafened ? "Deafened" : "Not deafened";
    }

    private void SetLastAction(string value)
    {
        _lastActionValueLabel.Text = value;
        _notifyIcon.Text = _monitoring ? "Auto Deafen osu! - monitoring" : "Auto Deafen osu!";
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private string FormatTelemetryError(Exception ex)
    {
        if (ex is TaskCanceledException or TimeoutException)
        {
            return $"gosumemory timed out. Open {_settings.GosumemoryEndpoint} in a browser and check that JSON loads.";
        }

        if (ex is HttpRequestException)
        {
            return $"Cannot connect to gosumemory. Start gosumemory, then open {_settings.GosumemoryEndpoint} in a browser.";
        }

        return $"Could not read gosumemory: {ex.Message}";
    }
}
