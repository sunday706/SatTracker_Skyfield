using System.Configuration;
using System.Globalization;
using AntenControl;

namespace SatTracker
{
    public sealed class SettingsForm : Form
    {
        private readonly Dictionary<string, Control> _inputs = new();
        private readonly MainForm? _mainForm;
        private readonly System.Windows.Forms.Timer _homeRefreshTimer = new();

        private Label? _lblRawAzi;
        private Label? _lblRawEle;
        private Label? _lblSystemAzi;
        private Label? _lblSystemEle;
        private Label? _lblAziServoStatus;
        private Label? _lblEleServoStatus;
        private Label? _lblAziEncoderStatus;
        private Label? _lblEleEncoderStatus;

        private readonly List<Button> _aziJogButtons = new();
        private readonly List<Button> _eleJogButtons = new();
        private Button? _btnSetAziHome;
        private Button? _btnSetEleHome;
        private Button? _btnTuneAziPid;
        private Button? _btnTuneElePid;
        private Label? _lblPidTuneStatus;
        private bool _pidAutoTuneBusy;

        public SettingsForm()
            : this(null)
        {
        }

        public SettingsForm(MainForm? mainForm)
        {
            _mainForm = mainForm;
            InitializeLayout();
            LoadSettings();
            RefreshHomeReadout();
            _homeRefreshTimer.Interval = 200;
            _homeRefreshTimer.Tick += (_, __) => RefreshHomeReadout();
            _homeRefreshTimer.Start();
        }

        private void InitializeLayout()
        {
            Text = "Antenna System Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 720);

            var tabs = new TabControl
            {
                Dock = DockStyle.Top,
                Height = 670
            };

            tabs.TabPages.Add(CreateComPage());
            tabs.TabPages.Add(CreateHomePage());
            tabs.TabPages.Add(CreateMotionPage());
            tabs.TabPages.Add(CreatePidPage());

            var btnSave = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Width = 90,
                Height = 32,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(ClientSize.Width - 200, 672)
            };
            btnSave.Click += (_, e) =>
            {
                if (!SaveSettings())
                {
                    DialogResult = DialogResult.None;
                }
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(ClientSize.Width - 100, 672)
            };

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            Controls.Add(tabs);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
        }

        private TabPage CreateComPage()
        {
            var page = CreatePage("COM Port");
            AddTextBox(page, "AziServoCom", "COM Servo Azimuth", "COM8", 24);
            AddTextBox(page, "EleServoCom", "COM Servo Elevation", "COM9", 72);
            AddTextBox(page, "EncoderCom", "COM Encoder", "COM3", 120);
            AddNumberBox(page, "ModbusBaud", "Baudrate Servo Modbus", 19200, 1200, 1000000, 168, 0);
            AddNumberBox(page, "EncoderBaud", "Baudrate Encoder", 115200, 1200, 1000000, 216, 0);
            AddCheckBox(page, "IgnoreModbusTimeoutErrors", "Ignore transient Modbus timeout", false, 264);
            return page;
        }

        private TabPage CreateHomePage()
        {
            var page = CreatePage("Home Position");
            AddNumberBox(page, "AziHomeDeg", "Set Anten Azimuth to (deg)", 0, -360, 360, 24, 3);
            AddNumberBox(page, "EleHomeDeg", "Set Anten Elevation to (deg)", 0, -90, 180, 72, 3);

            page.Controls.Add(CreateLabel("Angle Encoder Azimuth", 128));
            _lblRawAzi = CreateValueLabel(128);
            page.Controls.Add(_lblRawAzi);

            page.Controls.Add(CreateLabel("Angle Encoder Elevation", 176));
            _lblRawEle = CreateValueLabel(176);
            page.Controls.Add(_lblRawEle);

            page.Controls.Add(CreateLabel("Angle Anten Azimuth", 224));
            _lblSystemAzi = CreateValueLabel(224);
            page.Controls.Add(_lblSystemAzi);

            page.Controls.Add(CreateLabel("Angle Anten Elevation", 272));
            _lblSystemEle = CreateValueLabel(272);
            page.Controls.Add(_lblSystemEle);

            page.Controls.Add(CreateLabel("Servo Azi Status", 320));
            _lblAziServoStatus = CreateStatusLabel(320);
            page.Controls.Add(_lblAziServoStatus);

            page.Controls.Add(CreateLabel("Encoder Azi Status", 352));
            _lblAziEncoderStatus = CreateStatusLabel(352);
            page.Controls.Add(_lblAziEncoderStatus);

            page.Controls.Add(CreateLabel("Servo Ele Status", 384));
            _lblEleServoStatus = CreateStatusLabel(384);
            page.Controls.Add(_lblEleServoStatus);

            page.Controls.Add(CreateLabel("Encoder Ele Status", 416));
            _lblEleEncoderStatus = CreateStatusLabel(416);
            page.Controls.Add(_lblEleEncoderStatus);

            AddJogButton(page, "Azi -", 450, 126, Manual_Control.Axis.Azimuth, -1);
            AddJogButton(page, "Azi +", 520, 126, Manual_Control.Axis.Azimuth, 1);
            AddJogButton(page, "Ele -", 450, 174, Manual_Control.Axis.Elevation, -1);
            AddJogButton(page, "Ele +", 520, 174, Manual_Control.Axis.Elevation, 1);

            _btnSetAziHome = new Button
            {
                Text = "Set Home Azi",
                Width = 120,
                Height = 30,
                Location = new Point(450, 222),
                Enabled = _mainForm != null
            };
            _btnSetAziHome.Click += (_, __) =>
            {
                SaveSettings();
                _mainForm?.SetAziHomeFromCurrent(ReadFloatInput("AziHomeDeg", 0));
                RefreshHomeReadout();
            };
            page.Controls.Add(_btnSetAziHome);

            _btnSetEleHome = new Button
            {
                Text = "Set Home Ele",
                Width = 120,
                Height = 30,
                Location = new Point(450, 270),
                Enabled = _mainForm != null
            };
            _btnSetEleHome.Click += (_, __) =>
            {
                SaveSettings();
                _mainForm?.SetEleHomeFromCurrent(ReadFloatInput("EleHomeDeg", 0));
                RefreshHomeReadout();
            };
            page.Controls.Add(_btnSetEleHome);

            return page;
        }

        private TabPage CreateMotionPage()
        {
            var page = CreatePage("Motion");
            AddNumberBox(page, "DefaultRpm", "Default RPM", 300, 1, 4100, 24, 0);
            AddNumberBox(page, "MaxRpm", "Max RPM", 2000, 1, 4100, 72, 0);
            AddNumberBox(page, "Acceleration", "Acceleration", 100, 0, 100000, 120, 3);
            AddNumberBox(page, "Deceleration", "Deceleration", 100, 0, 100000, 168, 3);
            AddNumberBox(page, "TrackingMinElevationDeg", "Tracking Elevation Min (deg)", 10, -90, 180, 216, 3);
            AddNumberBox(page, "GoToleranceDeg", "GO Tolerance (deg)", 0.2m, 0.01m, 10, 264, 3);
            AddNumberBox(page, "GoTimeoutSec", "GO Timeout (sec)", 60, 1, 3600, 312, 0);
            return page;
        }

        private TabPage CreatePidPage()
        {
            var page = CreatePage("PID");
            page.Controls.Add(CreateSectionLabel("Azimuth PID", 24));
            AddNumberBox(page, "AziPidKp", "Azimuth Kp", 20, 0, 100000, 64, 4);
            AddNumberBox(page, "AziPidKi", "Azimuth Ki", 0, 0, 100000, 112, 4);
            AddNumberBox(page, "AziPidKd", "Azimuth Kd", 0, 0, 100000, 160, 4);
            _btnTuneAziPid = CreateAutoTuneButton(64, Manual_Control.Axis.Azimuth);
            page.Controls.Add(_btnTuneAziPid);

            page.Controls.Add(CreateSectionLabel("Elevation PID", 232));
            AddNumberBox(page, "ElePidKp", "Elevation Kp", 20, 0, 100000, 272, 4);
            AddNumberBox(page, "ElePidKi", "Elevation Ki", 0, 0, 100000, 320, 4);
            AddNumberBox(page, "ElePidKd", "Elevation Kd", 0, 0, 100000, 368, 4);
            _btnTuneElePid = CreateAutoTuneButton(272, Manual_Control.Axis.Elevation);
            page.Controls.Add(_btnTuneElePid);

            page.Controls.Add(CreateSectionLabel("Auto Tune", 408));
            AddNumberBox(page, "PidAutoTuneHysteresisDeg", "Hysteresis (deg)", 1, 0.05m, 30, 440, 2);
            AddNumberBox(page, "PidAutoTuneMaxTravelDeg", "Max Travel (deg)", 20, 1, 180, 488, 2);
            AddNumberBox(page, "PidAutoTuneRpm", "Tune RPM", 80, 1, 4100, 536, 0);

            _lblPidTuneStatus = new Label
            {
                Text = "Auto tune status: idle",
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 546,
                Height = 56,
                Location = new Point(24, 584)
            };
            page.Controls.Add(_lblPidTuneStatus);
            return page;
        }

        private static TabPage CreatePage(string title)
        {
            return new TabPage(title)
            {
                Padding = new Padding(16),
                BackColor = SystemColors.Control
            };
        }

        private void AddTextBox(Control parent, string key, string label, string defaultValue, int top)
        {
            var lbl = CreateLabel(label, top);
            var input = new TextBox
            {
                Name = key,
                Text = defaultValue,
                Width = 180,
                Location = new Point(250, top - 3)
            };

            _inputs[key] = input;
            parent.Controls.Add(lbl);
            parent.Controls.Add(input);
        }

        private void AddNumberBox(Control parent, string key, string label, decimal defaultValue,
            decimal min, decimal max, int top, int decimalPlaces)
        {
            var lbl = CreateLabel(label, top);
            var input = new NumericUpDown
            {
                Name = key,
                DecimalPlaces = decimalPlaces,
                Minimum = min,
                Maximum = max,
                Value = Math.Clamp(defaultValue, min, max),
                Width = 180,
                Location = new Point(250, top - 3),
                ThousandsSeparator = true
            };

            _inputs[key] = input;
            parent.Controls.Add(lbl);
            parent.Controls.Add(input);
        }

        private void AddCheckBox(Control parent, string key, string label, bool defaultValue, int top)
        {
            var input = new CheckBox
            {
                Name = key,
                Text = label,
                Checked = defaultValue,
                AutoSize = false,
                Width = 360,
                Height = 26,
                Location = new Point(250, top - 3)
            };

            _inputs[key] = input;
            parent.Controls.Add(input);
        }

        private Button CreateAutoTuneButton(int top, Manual_Control.Axis axis)
        {
            var button = new Button
            {
                Text = "Auto Tune",
                Width = 120,
                Height = 30,
                Location = new Point(450, top - 3),
                Enabled = _mainForm != null
            };

            button.Click += async (_, __) => await RunPidAutoTuneAsync(axis);
            return button;
        }

        private static Label CreateLabel(string text, int top)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 220,
                Height = 26,
                Location = new Point(24, top)
            };
        }

        private static Label CreateSectionLabel(string text, int top)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 220,
                Height = 26,
                Location = new Point(24, top)
            };
        }

        private static Label CreateValueLabel(int top)
        {
            return new Label
            {
                Text = "N/A",
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 180,
                Height = 26,
                Location = new Point(250, top)
            };
        }

        private static Label CreateStatusLabel(int top)
        {
            return new Label
            {
                Text = "N/A",
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 180,
                Height = 26,
                Location = new Point(250, top)
            };
        }

        private void AddJogButton(Control parent, string text, int left, int top, Manual_Control.Axis axis, int direction)
        {
            var button = new Button
            {
                Text = text,
                Width = 60,
                Height = 30,
                Location = new Point(left, top),
                Enabled = _mainForm != null
            };

            button.MouseDown += (_, __) => _mainForm?.StartSettingsJog(axis, direction);
            button.MouseUp += (_, __) => _mainForm?.StopSettingsJog(axis);
            button.MouseLeave += (_, __) => _mainForm?.StopSettingsJog(axis);
            parent.Controls.Add(button);

            if (axis == Manual_Control.Axis.Azimuth)
            {
                _aziJogButtons.Add(button);
            }
            else
            {
                _eleJogButtons.Add(button);
            }
        }

        private float ReadFloatInput(string key, float defaultValue)
        {
            if (_inputs.TryGetValue(key, out var control) &&
                control is NumericUpDown numberBox)
            {
                return (float)numberBox.Value;
            }

            return defaultValue;
        }

        private void RefreshHomeReadout()
        {
            if (_mainForm == null) return;

            if (_lblRawAzi != null) _lblRawAzi.Text = _mainForm.CurrentRawAziText;
            if (_lblRawEle != null) _lblRawEle.Text = _mainForm.CurrentRawEleText;
            if (_lblSystemAzi != null) _lblSystemAzi.Text = _mainForm.CurrentSystemAziText;
            if (_lblSystemEle != null) _lblSystemEle.Text = _mainForm.CurrentSystemEleText;

            bool aziServoReady = _mainForm.IsServoReady(Manual_Control.Axis.Azimuth);
            bool eleServoReady = _mainForm.IsServoReady(Manual_Control.Axis.Elevation);
            bool aziEncoderReady = _mainForm.IsEncoderReady(Manual_Control.Axis.Azimuth);
            bool eleEncoderReady = _mainForm.IsEncoderReady(Manual_Control.Axis.Elevation);

            if (_lblAziServoStatus != null) _lblAziServoStatus.Text = _mainForm.GetServoStatus(Manual_Control.Axis.Azimuth);
            if (_lblEleServoStatus != null) _lblEleServoStatus.Text = _mainForm.GetServoStatus(Manual_Control.Axis.Elevation);
            if (_lblAziEncoderStatus != null) _lblAziEncoderStatus.Text = _mainForm.GetEncoderStatus(Manual_Control.Axis.Azimuth);
            if (_lblEleEncoderStatus != null) _lblEleEncoderStatus.Text = _mainForm.GetEncoderStatus(Manual_Control.Axis.Elevation);

            foreach (var button in _aziJogButtons) button.Enabled = aziServoReady;
            foreach (var button in _eleJogButtons) button.Enabled = eleServoReady;
            if (_btnSetAziHome != null) _btnSetAziHome.Enabled = aziEncoderReady;
            if (_btnSetEleHome != null) _btnSetEleHome.Enabled = eleEncoderReady;
            if (_btnTuneAziPid != null) _btnTuneAziPid.Enabled = !_pidAutoTuneBusy && aziServoReady && aziEncoderReady;
            if (_btnTuneElePid != null) _btnTuneElePid.Enabled = !_pidAutoTuneBusy && eleServoReady && eleEncoderReady;
        }

        private async Task RunPidAutoTuneAsync(Manual_Control.Axis axis)
        {
            if (_mainForm == null || _pidAutoTuneBusy) return;

            string axisName = axis == Manual_Control.Axis.Azimuth ? "Azimuth" : "Elevation";
            var confirm = MessageBox.Show(
                $"Auto Tune PID {axisName} will move the antenna around the current position. Make sure the axis has enough free travel.",
                "PID Auto Tune",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.OK) return;

            _pidAutoTuneBusy = true;
            RefreshHomeReadout();
            if (_lblPidTuneStatus != null) _lblPidTuneStatus.Text = $"Auto tune {axisName}: running...";

            try
            {
                var result = await _mainForm.AutoTunePidAsync(axis, ReadPidAutoTuneOptions());
                if (result.Success)
                {
                    ApplyPidTuneResult(result);
                    if (_lblPidTuneStatus != null)
                    {
                        _lblPidTuneStatus.Text =
                            $"{axisName}: Kp={result.Kp:F4}, Ki={result.Ki:F4}, Kd={result.Kd:F4}; Tu={result.UltimatePeriodSec:F3}s, Amp={result.OscillationAmplitudeDeg:F3}deg";
                    }
                }
                else
                {
                    if (_lblPidTuneStatus != null) _lblPidTuneStatus.Text = $"{axisName}: {result.Message}";
                    MessageBox.Show(result.Message, "PID Auto Tune", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                if (_lblPidTuneStatus != null) _lblPidTuneStatus.Text = $"{axisName}: auto tune failed.";
                MessageBox.Show($"PID auto tune failed: {ex.Message}", "PID Auto Tune",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _pidAutoTuneBusy = false;
                RefreshHomeReadout();
            }
        }

        private Manual_Control.PidAutoTuneOptions ReadPidAutoTuneOptions()
        {
            float hysteresisDeg = ReadFloatInput("PidAutoTuneHysteresisDeg", 1f);
            float maxTravelDeg = ReadFloatInput("PidAutoTuneMaxTravelDeg", 20f);
            int testRpm = Math.Max(1, (int)Math.Round(ReadFloatInput("PidAutoTuneRpm", 80f)));

            return new Manual_Control.PidAutoTuneOptions
            {
                HysteresisDeg = hysteresisDeg,
                MaxTravelDeg = Math.Max(maxTravelDeg, hysteresisDeg + 1f),
                TestRpm = testRpm
            };
        }

        private void ApplyPidTuneResult(Manual_Control.PidAutoTuneResult result)
        {
            string prefix = result.Axis == Manual_Control.Axis.Azimuth ? "Azi" : "Ele";
            SetNumberInput($"{prefix}PidKp", result.Kp);
            SetNumberInput($"{prefix}PidKi", result.Ki);
            SetNumberInput($"{prefix}PidKd", result.Kd);
        }

        private void SetNumberInput(string key, float value)
        {
            if (_inputs.TryGetValue(key, out var control) && control is NumericUpDown numberBox)
            {
                decimal decimalValue = (decimal)Math.Clamp(value, (float)numberBox.Minimum, (float)numberBox.Maximum);
                numberBox.Value = decimal.Round(decimalValue, numberBox.DecimalPlaces);
            }
        }

        private void LoadSettings()
        {
            foreach (var (key, control) in _inputs)
            {
                string? value = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = ConfigurationManager.AppSettings[GetLegacyPidKey(key)];
                }
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (control is TextBox textBox)
                {
                    textBox.Text = value;
                }
                else if (control is NumericUpDown numberBox &&
                         decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    numberBox.Value = Math.Clamp(parsed, numberBox.Minimum, numberBox.Maximum);
                }
                else if (control is CheckBox checkBox &&
                         bool.TryParse(value, out var checkedValue))
                {
                    checkBox.Checked = checkedValue;
                }
            }
        }

        private static string GetLegacyPidKey(string key)
        {
            return key switch
            {
                "AziPidKp" or "ElePidKp" => "PidKp",
                "AziPidKi" or "ElePidKi" => "PidKi",
                "AziPidKd" or "ElePidKd" => "PidKd",
                _ => string.Empty
            };
        }

        private bool SaveSettings()
        {
            try
            {
                var configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                foreach (var (key, control) in _inputs)
                {
                    string value = control switch
                    {
                        TextBox textBox => textBox.Text.Trim(),
                        NumericUpDown numberBox => numberBox.Value.ToString(CultureInfo.InvariantCulture),
                        CheckBox checkBox => checkBox.Checked.ToString(CultureInfo.InvariantCulture),
                        _ => string.Empty
                    };

                    if (configuration.AppSettings.Settings[key] == null)
                    {
                        configuration.AppSettings.Settings.Add(key, value);
                    }
                    else
                    {
                        configuration.AppSettings.Settings[key].Value = value;
                    }
                }

                configuration.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                _mainForm?.ApplyEncoderHomeCalibration();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot save configuration: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _homeRefreshTimer.Stop();
                _homeRefreshTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
