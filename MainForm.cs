using AntenControl;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Newtonsoft.Json;
using One_Sgp4;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SatTracker.AntenControl;
using System;
using System.Configuration;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;


namespace SatTracker
{
    public partial class MainForm : Form
    {
        // Tọa độ địa lý
        private double observerLat = 21.0285;   // Default: Hanoi
        private double observerLon = 105.8542;
        private double observerAlt = 0.03;      // in km
        private string udpAddress = "192.168.0.1";//Địa chỉ UDP muốn gửi data
        private UdpClient udpClientReceive;
        private int receivePort = 1000, sendPort = 1000;
        private string tleFilePath = "SatTLE.txt"; // Đường dẫn file TLE mặc định, có thể thay đổi
        private GMapOverlay satOverlay = new("satelliteOverlay");
        private GMapOverlay routesOverlay = new GMapOverlay("routes");
        private List<SatelliteVisible> visibilityList = new List<SatelliteVisible>();
        private List<SatelliteInfo> informationList = new List<SatelliteInfo>();
        private SatelliteInfo selectedSatellite;
        private List<DateTime> timeStampsSend;
        private List<double> azimuthAnglesSend, elevationAnglesSend, latitudeAngles, longtitudeAngles, SolarAzimuthSend, SolarelEvationSend, RelativeAzimuthSend, RelativeElevationSend;
        private double[] azimuthAnglesRecei, elevationAnglesRecei;
        private double currentElevation = 0; // Biến lưu trữ giá trị elevation hiện tại
        private double currentAzimuth = 0;   // Biến lưu trữ giá trị azimuth hiện tại
        private double elevationStep = 1.0; // Giá trị thay đổi elevation mỗi lần nhấn (điều chỉnh theo ý muốn)
        private double azimuthStep = 1.0;    // Giá trị thay đổi azimuth mỗi lần nhấn (điều chỉnh theo ý muốn)
        private int indexSend = 0, indexRecei = 0;
        private StringBuilder resultBuilder = new StringBuilder();

        // Manual control object for antenna control via serial
        private Manual_Control? _manual;

        // Encoder reader for receiving data
        private EncoderComReader? _encReader;
        private System.Windows.Forms.Timer? _antennaTrackingTimer;
        private System.Windows.Forms.Timer? _pendingTrackingTimer;
        private bool _antennaTrackingBusy;
        private bool _pendingPrepositionBusy;
        private bool _trackingPausedBelowElevationLimit;
        private ControlMode _controlMode = ControlMode.Manual;
        private TrackingTarget _activeTrackingTarget = TrackingTarget.Satellite;
        private DateTime? _pendingTrackingStartTime;
        private string? _pendingTrackingSatelliteName;

        private enum ControlMode
        {
            Manual,
            Tracking
        }

        private enum TrackingTarget
        {
            Satellite,
            Sun
        }

        private enum TrackingDataLoadStatus
        {
            Ready,
            Pending,
            Expired,
            Invalid
        }

        private string RuntimeDataPath(string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        private float GetFloatSetting(string name, float defaultValue)
        {
            var strValue = ConfigurationManager.AppSettings[name];
            float result;
            if (float.TryParse(strValue, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return defaultValue;
        }
        private int GetIntSetting(string name, int defaultValue)
        {
            var strValue = ConfigurationManager.AppSettings[name];
            int result;
            if (int.TryParse(strValue, out result))
            {
                return result;
            }
            return defaultValue;
        }
        private string GetStringSetting(string name, string defaultValue)
        {
            var result = ConfigurationManager.AppSettings[name];
            if (string.IsNullOrEmpty(result))
            {
                return defaultValue;
            }
            return result;
        }
        private double GetDoubleSetting(string name, double defaultValue)
        {
            var strValue = ConfigurationManager.AppSettings[name];
            double result;
            if (double.TryParse(strValue, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return defaultValue;
        }
        private Manual_Control.MotionParameters GetMotionParameters()
        {
            return new Manual_Control.MotionParameters
            {
                DefaultRpm = Math.Max(1, GetIntSetting("DefaultRpm", 300)),
                MaxRpm = Math.Clamp(GetIntSetting("MaxRpm", 2000), 1, 4100),
                Acceleration = Math.Max(0f, GetFloatSetting("Acceleration", 100)),
                Deceleration = Math.Max(0f, GetFloatSetting("Deceleration", 100)),
                TrackingMinRpm = Math.Max(1, GetIntSetting("TrackingMinRpm", 30)),
                PidKp = Math.Max(0f, GetFloatSetting("PidKp", 25)),
                PidKi = Math.Max(0f, GetFloatSetting("PidKi", 0.5f)),
                PidKd = Math.Max(0f, GetFloatSetting("PidKd", 0.01f))
            };
        }
        private void SaveSetting(string key, object value)
        {
            var str = Convert.ToString(value, CultureInfo.InvariantCulture);
            SaveSetting(key, str);
        }
        private void SaveSetting(string key, string value)
        {
            var configurationFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configurationFile.AppSettings.Settings.Remove(key);
            configurationFile.AppSettings.Settings.Add(key, value);
            configurationFile.Save(ConfigurationSaveMode.Full);
            ConfigurationManager.RefreshSection("appSettings");
        }
        private static byte[] ScaledDoubleToBytes(double value)
        {
            double scaled = value * 1000;
            return BitConverter.GetBytes(scaled);
        }
        private static byte[] TwoScaledDoublesToBytes(double value1, double value2)
        {
            double scaled1 = value1 * 1000;
            double scaled2 = value2 * 1000;

            byte[] bytes1 = BitConverter.GetBytes(scaled1);
            byte[] bytes2 = BitConverter.GetBytes(scaled2);

            // Gộp 2 mảng byte
            byte[] result = new byte[16];
            Buffer.BlockCopy(bytes1, 0, result, 0, 8);
            Buffer.BlockCopy(bytes2, 0, result, 8, 8);
            return result;
        }
        private static byte[] ScaledFloadToBytes(float value)
        {
            float scaled = value * 1000;
            return BitConverter.GetBytes(scaled);
        }
        private static byte[] TwoScaledFloadToBytes(float value1, float value2)
        {
            float scaled1 = value1 * 1000;
            float scaled2 = value2 * 1000;

            byte[] bytes1 = BitConverter.GetBytes(scaled1);
            byte[] bytes2 = BitConverter.GetBytes(scaled2);

            // Gộp 2 mảng byte
            byte[] result = new byte[8];
            Buffer.BlockCopy(bytes1, 0, result, 0, 4);
            Buffer.BlockCopy(bytes2, 0, result, 4, 4);
            return result;
        }
        private void dataGridViewSatellites_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv.Columns.Contains("Name"))
                dgv.Columns["Name"].HeaderText = "Tên VT";
            if (dgv.Columns.Contains("StartVisibleTime"))
                dgv.Columns["StartVisibleTime"].HeaderText = "Thời gian bắt đầu";
            if (dgv.Columns.Contains("EndVisibleTime"))
                dgv.Columns["EndVisibleTime"].HeaderText = "Thời gian kết thúc";
        }
        private void dataGridInforSatellites_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv.Columns.Contains("Name"))
                dgv.Columns["Name"].HeaderText = "Tên VT";
            if (dgv.Columns.Contains("StartVisibleTime"))
                dgv.Columns["StartVisibleTime"].HeaderText = "Thời gian bắt đầu";
            if (dgv.Columns.Contains("EndVisibleTime"))
                dgv.Columns["EndVisibleTime"].HeaderText = "Thời gian kết thúc";
        }
        private void dataGridViewSatellites_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = sender as DataGridView;

            if (dgv.Columns[e.ColumnIndex].Name == "StartVisibleTime")
            {
                if (e.Value is DateTime dt)
                {
                    e.Value = dt.ToString("HH:mm:ss - dd/MM/yyyy");
                    e.FormattingApplied = true;
                }
            }

            if (dgv.Columns[e.ColumnIndex].Name == "EndVisibleTime")
            {
                if (e.Value is DateTime dt)
                {
                    e.Value = dt.ToString("HH:mm:ss - dd/MM/yyyy");
                    e.FormattingApplied = true;
                }
            }
        }
        private void dataGridInforSatellites_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = sender as DataGridView;

            if (dgv.Columns[e.ColumnIndex].Name == "StartVisibleTime")
            {
                if (e.Value is DateTime dt)
                {
                    e.Value = dt.ToString("HH:mm:ss - dd/MM/yyyy");
                    e.FormattingApplied = true;
                }
            }

            if (dgv.Columns[e.ColumnIndex].Name == "EndVisibleTime")
            {
                if (e.Value is DateTime dt)
                {
                    e.Value = dt.ToString("HH:mm:ss - dd/MM/yyyy");
                    e.FormattingApplied = true;
                }
            }
        }
        private void dataGridViewSatellites_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            var rowIdx = (e.RowIndex + 1).ToString();

            var centerFormat = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            var headerBounds = new Rectangle(
                e.RowBounds.Left,
                e.RowBounds.Top,
                grid.RowHeadersWidth,
                e.RowBounds.Height);

            e.Graphics.DrawString(rowIdx,
                this.Font,
                SystemBrushes.ControlText,
                headerBounds,
                centerFormat);
        }
        private void dataGridInforSatellites_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            var rowIdx = (e.RowIndex + 1).ToString();

            var centerFormat = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            var headerBounds = new Rectangle(
                e.RowBounds.Left,
                e.RowBounds.Top,
                grid.RowHeadersWidth,
                e.RowBounds.Height);

            e.Graphics.DrawString(rowIdx,
                this.Font,
                SystemBrushes.ControlText,
                headerBounds,
                centerFormat);
        }
        private async void Form1_Load(object sender, EventArgs e)
        {
            dataGridViewSatellites.DataBindingComplete += dataGridViewSatellites_DataBindingComplete;
            dataGridViewSatellites.RowPostPaint += dataGridViewSatellites_RowPostPaint;
            dataGridViewSatellites.RowHeadersVisible = true;
            dataGridViewSatellites.CellFormatting += dataGridViewSatellites_CellFormatting;

            dataGridInforSatellites.DataBindingComplete += dataGridInforSatellites_DataBindingComplete;
            dataGridInforSatellites.RowPostPaint += dataGridInforSatellites_RowPostPaint;
            dataGridInforSatellites.RowHeadersVisible = true;
            dataGridInforSatellites.CellFormatting += dataGridInforSatellites_CellFormatting;

            string aziServoCom = GetStringSetting("AziServoCom", "COM8");
            string eleServoCom = GetStringSetting("EleServoCom", "COM9");
            string encoderCom = GetStringSetting("EncoderCom", "COM3");
            int modbusBaud = GetIntSetting("ModbusBaud", 19200);
            int encoderBaud = GetIntSetting("EncoderBaud", 115200);
            int defaultRpm = GetIntSetting("DefaultRpm", 300);
            float aziHomeRawDeg = GetFloatSetting("AziHomeRawDeg", 0);
            float eleHomeRawDeg = GetFloatSetting("EleHomeRawDeg", 0);
            float aziHomeDeg = GetFloatSetting("AziHomeDeg", 0);
            float eleHomeDeg = GetFloatSetting("EleHomeDeg", 0);

            txbAziSpeed.Text = defaultRpm.ToString(CultureInfo.InvariantCulture);
            txbEleSpeed.Text = defaultRpm.ToString(CultureInfo.InvariantCulture);

            _manual = new Manual_Control(
                this,
                btnAziLeft, btnAziRight, btnEleUp, btnEleDown,

                new Manual_Control.AxisUi
                {
                    BtnConnect = btnAziConnect,
                    BtnEnable = btnAziEnable,
                    TxbSpeedRpm = txbAziSpeed,
                    TxbPositionDeg = txbAziPos,
                    LblStatus = lblAziStt,
                    TxbTargetPosDeg = txbAziTargetPos,
                    BtnGo = btnAziGo
                },

                new Manual_Control.AxisUi
                {
                    BtnConnect = btnEleConnect,
                    BtnEnable = btnEleEnable,
                    TxbSpeedRpm = txbEleSpeed,
                    TxbPositionDeg = txbElePos,
                    LblStatus = lblEleStt,
                    TxbTargetPosDeg = txbEleTargetPos,
                    BtnGo = btnEleGo
                },

                azCom: aziServoCom,
                elCom: eleServoCom,
                azId: 1,
                elId: 1,
                defaultRpm: defaultRpm,
                modbusBaud: modbusBaud,
                motionParametersProvider: GetMotionParameters
            );
            _manual.StateChanged += UpdateTrackingButtonAvailability;
            _manual.SetUiLocked(_controlMode == ControlMode.Tracking);
            UpdateTrackingButtonAvailability();

            _encReader = new EncoderComReader(this, txbAziPos, txbElePos, encoderCom, encoderBaud,
                aziHomeRawDeg, eleHomeRawDeg, aziHomeDeg, eleHomeDeg);
        }
        public MainForm()
        {
            InitializeComponent();
            btnSetting.Click += btnSetting_Click;
            btnStop.Click += btnStop_Click;
            btnTraking.Click += btnTraking_Click;
            btnGoHome.Click += btnGoHome_Click;
            ApplyControlMode(ControlMode.Manual);
            SetManualButtonsEnabled(false);
            InitMap();
            // Tọa độ vị trí quan sát
            observerLat = GetDoubleSetting("Latitude", 21.03); // Hà Nội
            observerLon = GetDoubleSetting("Longitude", 105.85);

            observerAlt = GetDoubleSetting("Altitude", 0.3); // in km
            udpAddress = GetStringSetting("AddresstoSend", "192.168.0.1"); //Địa chỉ UDP muốn gửi data

            receivePort = GetIntSetting("PortSend", 1000); // Port gửi
            sendPort = GetIntSetting("PortReceive", 1000); // Port nhận
            udpClientReceive = new UdpClient(receivePort);
            MainTimer.Interval = 15 * 60 * 1000; // 15 phút (1800000 ms)

            GetSatelliteVisibility();  // Gọi hàm lấy dữ liệu vệ tinh
            GetSatelliteInformation(); // Gọi hàm lấy thông tin vệ tinh
            LoadSatelliteData();  // Gọi hàm load dữ liệu vệ tinh
        }

        private void btnSetting_Click(object? sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(this);
            if (settingsForm.ShowDialog(this) != DialogResult.OK) return;

            int defaultRpm = GetIntSetting("DefaultRpm", 300);
            txbAziSpeed.Text = defaultRpm.ToString(CultureInfo.InvariantCulture);
            txbEleSpeed.Text = defaultRpm.ToString(CultureInfo.InvariantCulture);

            MessageBox.Show(
                "Da luu cau hinh. Cac gia tri COM/baud se duoc ap dung khi khoi dong lai ung dung.",
                "Cau hinh",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void btnStop_Click(object? sender, EventArgs e)
        {
            ClearPendingTracking();
            StopAntennaTrajectoryTracking();
            if (_manual != null)
            {
                await _manual.StopAllMotionAsync();
            }
            ApplyControlMode(ControlMode.Manual);
            SetTrackingInfo("Đã dừng tracking. Chế độ điều khiển tay.");
        }

        private async void btnGoHome_Click(object? sender, EventArgs e)
        {
            ClearPendingTracking();
            StopAntennaTrajectoryTracking();
            ApplyControlMode(ControlMode.Manual);

            if (_manual == null)
            {
                SetTrackingInfo("Chưa khởi tạo điều khiển anten.");
                return;
            }

            if (!IsServoReady(Manual_Control.Axis.Azimuth) || !IsServoReady(Manual_Control.Axis.Elevation))
            {
                SetTrackingInfo("Cần Connect và Enable cả 2 servo trước khi GO HOME.");
                MessageBox.Show("Cần Connect và Enable cả 2 servo trước khi GO HOME.", "GO HOME",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsEncoderReady(Manual_Control.Axis.Azimuth) || !IsEncoderReady(Manual_Control.Axis.Elevation))
            {
                SetTrackingInfo("Cần Encoder Azimuth/Elevation có dữ liệu hợp lệ trước khi GO HOME.");
                MessageBox.Show("Cần Encoder Azimuth/Elevation có dữ liệu hợp lệ trước khi GO HOME.", "GO HOME",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SetTrackingInfo("Đang đưa anten về Home: Azimuth 0°, Elevation 90°...");
                btnGoHome.Enabled = false;

                await Task.WhenAll(
                    _manual.GoToTargetAsync(Manual_Control.Axis.Azimuth, 0f),
                    _manual.GoToTargetAsync(Manual_Control.Axis.Elevation, 90f));

                SetTrackingInfo("Đã đưa anten về Home: Azimuth 0°, Elevation 90°.");
            }
            catch (OperationCanceledException)
            {
                SetTrackingInfo("GO HOME đã bị hủy.");
            }
            catch (TimeoutException ex)
            {
                SetTrackingInfo($"GO HOME timeout: {ex.Message}. Kiểm tra COM, nguồn servo, baudrate, dây RS485/Modbus.");
            }
            catch (Exception ex)
            {
                SetTrackingInfo($"Lỗi GO HOME: {ex.Message}");
                MessageBox.Show($"Lỗi GO HOME: {ex.GetType().Name}: {ex.Message}", "GO HOME",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UpdateTrackingButtonAvailability();
            }
        }

        private void btnTraking_Click(object? sender, EventArgs e)
        {
            _activeTrackingTarget = TrackingTarget.Satellite;
            ApplyControlMode(ControlMode.Tracking);
            SetTrackingInfo("Chế độ tự động. Đang kiểm tra dữ liệu vệ tinh...");
            StartAntennaTrajectoryTracking();
        }

        private void btnSunTracking_Click(object? sender, EventArgs e)
        {
            _activeTrackingTarget = TrackingTarget.Sun;
            ApplyControlMode(ControlMode.Tracking);
            SetTrackingInfo("Chế độ tự động. Đang kiểm tra dữ liệu Mặt Trời...");
            StartAntennaTrajectoryTracking();
        }

        private void ApplyControlMode(ControlMode mode)
        {
            _controlMode = mode;
            bool trackingMode = mode == ControlMode.Tracking;

            ApplyControlModeToChildren(this, trackingMode);
            ApplyToolStripMode(trackingMode);

            btnStop.Enabled = true;
            UpdateTrackingButtonAvailability();
            lblSatus.Enabled = true;

            if (!trackingMode)
            {
                _manual?.SetUiLocked(false);
                _manual?.RefreshUiState();
            }
            else
            {
                _manual?.SetUiLocked(true);
            }
        }

        private void ApplyControlModeToChildren(Control parent, bool trackingMode)
        {
            foreach (Control child in parent.Controls)
            {
                ApplyControlModeToChildren(child, trackingMode);

                if (!IsModeManagedControl(child)) continue;
                child.Enabled = !trackingMode || ReferenceEquals(child, btnStop);
            }
        }

        private bool IsModeManagedControl(Control control)
        {
            return control is Button ||
                   control is TextBox ||
                   control is NumericUpDown ||
                   control is ComboBox ||
                   control is CheckBox ||
                   control is RadioButton ||
                   control is DataGridView ||
                   control is GMap.NET.WindowsForms.GMapControl ||
                   control is OxyPlot.WindowsForms.PlotView;
        }

        private void ApplyToolStripMode(bool trackingMode)
        {
            foreach (ToolStripItem item in toolStrip1.Items)
            {
                item.Enabled = !trackingMode;
            }
        }

        private void SetTrackingInfo(string message)
        {
            lblSatus.Text = message;
        }

        private void UpdateTrackingButtonAvailability()
        {
            if (_controlMode == ControlMode.Tracking)
            {
                btnTraking.Enabled = false;
                btnSunTracking.Enabled = false;
                btnGoHome.Enabled = false;
                return;
            }

            bool ready =
                IsServoReady(Manual_Control.Axis.Azimuth) &&
                IsServoReady(Manual_Control.Axis.Elevation);

            btnTraking.Enabled = ready;
            btnSunTracking.Enabled = ready;
            btnGoHome.Enabled = ready;
        }

        private void SetManualButtonsEnabled(bool enabled)
        {
            btnAziLeft.Enabled = enabled;
            btnAziRight.Enabled = enabled;
            btnEleUp.Enabled = enabled;
            btnEleDown.Enabled = enabled;
        }

        public string CurrentRawAziText => _encReader == null
            ? "N/A"
            : _encReader.RawAziDeg.ToString("0.###", CultureInfo.InvariantCulture);

        public string CurrentRawEleText => _encReader == null
            ? "N/A"
            : _encReader.RawEleDeg.ToString("0.###", CultureInfo.InvariantCulture);

        public string CurrentSystemAziText => _encReader == null
            ? txbAziPos.Text
            : _encReader.AziDeg.ToString("0.###", CultureInfo.InvariantCulture);

        public string CurrentSystemEleText => _encReader == null
            ? txbElePos.Text
            : _encReader.EleDeg.ToString("0.###", CultureInfo.InvariantCulture);

        public bool IsEncoderReady(Manual_Control.Axis axis)
        {
            if (_encReader == null) return false;
            return axis == Manual_Control.Axis.Azimuth
                ? _encReader.IsAziReady
                : _encReader.IsEleReady;
        }

        public string GetEncoderStatus(Manual_Control.Axis axis)
        {
            if (_encReader == null || !_encReader.IsConnected) return "Encoder not connected";
            if (!_encReader.HasValidData) return "Encoder no valid data";
            bool ready = axis == Manual_Control.Axis.Azimuth ? _encReader.IsAziReady : _encReader.IsEleReady;
            return ready ? "Encoder ready" : "Encoder error";
        }

        public bool IsServoReady(Manual_Control.Axis axis)
        {
            return _manual?.IsAxisEnabled(axis) == true;
        }

        public string GetServoStatus(Manual_Control.Axis axis)
        {
            return _manual?.GetAxisStatus(axis) ?? "Servo not initialized";
        }

        public void SetAziHomeFromCurrent(float homeDeg)
        {
            if (_encReader == null) return;
            SaveSetting("AziHomeRawDeg", _encReader.RawAziDeg);
            SaveSetting("AziHomeDeg", homeDeg);
            ApplyEncoderHomeCalibration();
        }

        public void SetEleHomeFromCurrent(float homeDeg)
        {
            if (_encReader == null) return;
            SaveSetting("EleHomeRawDeg", _encReader.RawEleDeg);
            SaveSetting("EleHomeDeg", homeDeg);
            ApplyEncoderHomeCalibration();
        }

        public void ApplyEncoderHomeCalibration()
        {
            _encReader?.ApplyHomeCalibration(
                GetFloatSetting("AziHomeRawDeg", 0),
                GetFloatSetting("EleHomeRawDeg", 0),
                GetFloatSetting("AziHomeDeg", 0),
                GetFloatSetting("EleHomeDeg", 0));
        }

        public void StartSettingsJog(Manual_Control.Axis axis, int direction)
        {
            if (_controlMode == ControlMode.Tracking) return;
            if (!IsServoReady(axis)) return;

            int rpm = Math.Max(1, GetIntSetting("DefaultRpm", 300));
            _ = _manual?.StartManualMoveAsync(axis, direction >= 0 ? rpm : -rpm);
        }

        public void StopSettingsJog(Manual_Control.Axis axis)
        {
            _ = _manual?.StopManualMoveAsync(axis);
        }

        // Hàm để thay đổi đường dẫn file TLE (gọi từ UI hoặc logic khác)
        public void SetTleFilePath(string newTleFilePath)
        {
            if (File.Exists(newTleFilePath))
            {
                tleFilePath = newTleFilePath;
                //File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Updated TLE file path to {tleFilePath}\n");
            }
            else
            {
                //File.AppendAllText("csharp_error.log", $"{DateTime.Now}: TLE file {newTleFilePath} does not exist\n");
                //MessageBox.Show($"File TLE {newTleFilePath} không tồn tại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSatelliteData()
        {
            try
            {
                RunPythonExecutable(tleFilePath);

                if (!File.Exists("satellite_visible.json") || !File.Exists("satellite_info.json"))
                {
                    //File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Missing JSON files\n");
                    //MessageBox.Show("Không tìm thấy file JSON.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string infoJson = File.ReadAllText("satellite_info.json", System.Text.Encoding.UTF8);
                //File.AppendAllText("csharp_error.log", $"{DateTime.Now}: satellite_info.json content: {infoJson.Length} characters\n");

                string visibleJson = File.ReadAllText("satellite_visible.json", System.Text.Encoding.UTF8);
                var visibleData = JsonConvert.DeserializeObject<List<SatelliteVisible>>(visibleJson);
                dataGridViewSatellites.DataSource = null;
                dataGridViewSatellites.DataSource = visibleData;
                dataGridViewSatellites.DataBindingComplete += dataGridViewSatellites_DataBindingComplete;

                informationList = JsonConvert.DeserializeObject<List<SatelliteInfo>>(infoJson);

                if (informationList.Count > 0)
                {
                    for (int i = 0; i < informationList.Count; i++)
                    {
                        double firstLatitudeAngles = 0;
                        if (informationList[i].LatitudeAngles != null && informationList[i].LatitudeAngles.Count > 0)
                        {
                            firstLatitudeAngles = informationList[i].LatitudeAngles[0];
                        }
                        double firstLongtitudeAngles = 0;
                        if (informationList[i].LongtitudeAngles != null && informationList[i].LongtitudeAngles.Count > 0)
                        {
                            firstLongtitudeAngles = informationList[i].LongtitudeAngles[0];
                        }
                        string firstName = "";
                        if (informationList[i].Name != null)
                        {
                            firstName = informationList[i].Name;
                        }

                        AddPoint(firstLatitudeAngles, firstLongtitudeAngles, firstName, Color.Blue, true, sizeInCm: 1);
                    }
                }

                /*
                var infoData = JsonConvert.DeserializeObject<List<SatelliteInfo>>(infoJson);
                var displayData = new List<SatelliteInfoDisplay>();

                foreach (var info in infoData)
                {
                    string satelliteName = info.Name.Replace("\ufeff", "");
                    File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Processing {satelliteName}, " +
                        $"TrackTimestamps: {(info.TrackTimestamps != null ? info.TrackTimestamps.Count : 0)}, " +
                        $"AzimuthAngles: {(info.AzimuthAngles != null ? $"[{string.Join(", ", info.AzimuthAngles)}]" : "null")}, " +
                        $"ElevationAngles: {(info.ElevationAngles != null ? $"[{string.Join(", ", info.ElevationAngles)}]" : "null")}\n");

                    if (info.TrackTimestamps != null && info.AzimuthAngles != null && info.ElevationAngles != null &&
                        info.TrackTimestamps.Count == info.AzimuthAngles.Count && info.AzimuthAngles.Count == info.ElevationAngles.Count &&
                        info.TrackTimestamps.Count > 0)
                    {
                        for (int i = 0; i < info.TrackTimestamps.Count; i++)
                        {
                            displayData.Add(new SatelliteInfoDisplay
                            {
                                Name = satelliteName,
                                Timestamp = info.TrackTimestamps[i],
                                Azimuth = info.AzimuthAngles[i],
                                Elevation = info.ElevationAngles[i],
                                Latitude = i < (info.LatitudeAngles?.Count ?? 0) ? info.LatitudeAngles[i] : 0.0,
                                Longitude = i < (info.LongitudeAngles?.Count ?? 0) ? info.LongitudeAngles[i] : 0.0
                            });
                        }
                    }
                    else
                    {
                        File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Skipping {satelliteName}: Invalid data " +
                            $"(TrackTimestamps={info.TrackTimestamps?.Count ?? 0}, AzimuthAngles={info.AzimuthAngles?.Count ?? 0}, ElevationAngles={info.ElevationAngles?.Count ?? 0})\n");
                    }
                }
                */
                dataGridInforSatellites.DataSource = null;
                dataGridInforSatellites.DataSource = informationList;// displayData;                
                dataGridInforSatellites.DataBindingComplete += dataGridInforSatellites_DataBindingComplete;
                //File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Displayed {displayData.Count} rows in DataGridViewInfo\n");
            }
            catch (Exception ex)
            {
                //File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Error in LoadSatelliteData: {ex.Message}\n");
                //MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunPythonExecutable(string tleFilePath)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = "satellite_passes.exe";
                start.Arguments = $"--tle-file \"{tleFilePath}\"";
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;

                File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Running satellite_passes.exe with TLE file: {tleFilePath}\n");

                using (Process process = Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Python executable error: {error}\n");
                        throw new Exception($"Lỗi chạy file thực thi: {error}");
                    }
                    if (!string.IsNullOrEmpty(output))
                    {
                        File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Python executable output: {output}\n");
                    }
                }
                // Chờ 1 giây để đảm bảo file JSON được ghi
                System.Threading.Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                File.AppendAllText("csharp_error.log", $"{DateTime.Now}: Error running Python executable: {ex.Message}\n");
                throw;
            }
        }
        private void InitMap()
        {
            // Cấu hình GMap
            gMap.MapProvider = GMapProviders.GoogleMap;
            GMaps.Instance.Mode = AccessMode.ServerOnly;

            // Vị trí ban đầu (Hà Nội)
            gMap.Position = new PointLatLng(observerLat, observerLon);
            gMap.MinZoom = 2;
            gMap.MaxZoom = 20;
            gMap.Zoom = 7;

            // Tạo overlay để chứa các markers
            gMap.Overlays.Add(satOverlay);
            // Tạo overlay để chứa các đường
            gMap.Overlays.Add(routesOverlay);

            // Cho phép kéo thả bản đồ
            gMap.SetPositionByKeywords("Vietnam");
            gMap.CanDragMap = true;
            gMap.DragButton = MouseButtons.Left;
            AddPoint(observerLat, observerLon, "Tọa độ gốc", Color.Red);
            // Zoom để hiển thị tất cả điểm
            ZoomToFitAllPoints();
        }
        // Phương thức thêm một điểm đơn lẻ
        public void AddPoint(double lat, double lng, string title = "", Color? color = null, bool isSatellite = false, double sizeInCm = 0.5)
        {
            PointLatLng point = new PointLatLng(lat, lng);
            GMapMarker marker;

            Color finalColor = color.HasValue ? color.Value : Color.Gold;

            if (isSatellite)
            {
                marker = new GMapSatelliteMarker(point, sizeInCm, finalColor, defaultZoom: 7);
            }
            else
            {
                GMarkerGoogleType markerType = color.HasValue ? GetGoogleMarkerType(finalColor) : GMarkerGoogleType.red_big_stop;
                marker = new GMarkerGoogle(point, markerType);
            }

            // CẤU HÌNH CHỮ SÁT VỆ TINH, KHÔNG NỀN, CỠ LỚN
            if (!string.IsNullOrEmpty(title))
            {
                marker.ToolTipText = title;

                // 1. Định dạng font chữ lớn và đậm
                marker.ToolTip.Font = new Font("Arial", 10, FontStyle.Bold);
                marker.ToolTip.Foreground = new SolidBrush(finalColor);

                // 2. Xóa bỏ hoàn toàn nền và viền khung
                marker.ToolTip.Fill = Brushes.Transparent;
                marker.ToolTip.Stroke = new Pen(Color.Transparent, 0);

                // 3. THU NHỎ PADDING (LỀ CHỮ)
                // Đặt về mức tối thiểu để chữ gom sát lại, không có khoảng trống thừa xung quanh
                marker.ToolTip.TextPadding = new Size(2, 2);

                // 4. ĐIỀU CHỈNH OFFSET ĐỂ KÉO CHỮ LẠI GẦN TÂM
                // Mặc định GMap đẩy chữ lên trên (-20). 
                // Ta chỉnh lại: X = 10 (lệch phải một chút để không đè lên thân), Y = -5 (sát ngay phía trên cánh vệ tinh)
                marker.ToolTip.Offset = new Point(10, -5);

                // Luôn luôn hiển thị chữ trên bản đồ
                marker.ToolTipMode = MarkerTooltipMode.Always;
            }

            satOverlay.Markers.Add(marker);
        }

        // Phương thức xóa tất cả điểm
        // Phương thức xóa tất cả điểm và đường
        public void ClearAllPoints()
        {
            satOverlay.Markers.Clear();
        }
        // Phương thức xóa tất cả đường
        public void ClearAllLines()
        {
            routesOverlay.Routes.Clear();
            routesOverlay.Polygons.Clear();
        }
        // Phương thức xóa tất cả
        public void ClearAll()
        {
            ClearAllPoints();
            ClearAllLines();
        }
        // Phương thức zoom để hiển thị tất cả điểm
        public void ZoomToFitAllPoints()
        {
            if (satOverlay.Markers.Count > 0)
            {
                gMap.ZoomAndCenterMarkers("satelliteOverlays");
            }
        }
        // Phương thức tính khoảng cách giữa 2 điểm (km)
        public double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            PointLatLng point1 = new PointLatLng(lat1, lng1);
            PointLatLng point2 = new PointLatLng(lat2, lng2);
            return GMap.NET.MapProviders.GMapProviders.EmptyProvider.Projection.GetDistance(point1, point2);
        }
        // Chuyển đổi màu sang loại marker Google
        private GMarkerGoogleType GetGoogleMarkerType(Color color)
        {
            if (color == Color.Red) return GMarkerGoogleType.red_pushpin;
            if (color == Color.Blue) return GMarkerGoogleType.blue_pushpin;
            if (color == Color.Green) return GMarkerGoogleType.green_pushpin;
            if (color == Color.Yellow) return GMarkerGoogleType.yellow_pushpin;
            return GMarkerGoogleType.red_pushpin; // Mặc định
        }
        // Phương thức vẽ đường nối nhiều điểm (polyline)
        public void DrawPolyline(List<PointLatLng> points, Color? lineColor = null,
            float lineWidth = 3f, string lineName = "")
        {
            if (points.Count < 2) return;

            GMapRoute route = new GMapRoute(points, lineName);
            route.Stroke = new Pen(lineColor ?? Color.Blue, lineWidth);

            routesOverlay.Routes.Add(route);
        }
        // Phương thức vẽ đường từ 2 mảng riêng biệt lat[] và long[]
        public void DrawLineFromArrays(List<double> latitudes, List<double> longitudes,
            Color? lineColor = null, int lineWidth = 3, string description = "")
        {
            if (latitudes == null || longitudes == null) return;
            if (latitudes.Count != longitudes.Count)
            {
                throw new ArgumentException("Mảng lat và long phải có cùng độ dài");
            }
            if (latitudes.Count < 2) return;

            List<PointLatLng> points = new List<PointLatLng>();

            for (int i = 0; i < latitudes.Count; i++)
            {
                points.Add(new PointLatLng(latitudes[i], longitudes[i]));
            }
            DrawPolyline(points, lineColor, lineWidth, description);
        }
        private void GetSatelliteVisibility()
        {
            var start = new EpochTime(DateTime.Now);
            Coordinate observer = new Coordinate(observerLat, observerLon, observerAlt);
            string[] lines = File.ReadAllLines(tleFilePath);
            for (int i = 0; i < lines.Length; i += 3)
            {
                if (i + 2 >= lines.Length) break;
                string name = lines[i].Trim();
                string line1 = lines[i + 1].Trim();
                string line2 = lines[i + 2].Trim();
                Tle tle = ParserTLE.parseTle(line1, line2, name);
                List<Pass> Passes = SatFunctions.CalculatePasses(observer, tle, start, 10, 1, Sgp4.wgsConstant.WGS_84);
                foreach (var pass in Passes)
                {
                    visibilityList.Add(new SatelliteVisible
                    {
                        Name = name,
                        StartTimeVisible = pass.getStartEpoch().toDateTime().ToLocalTime(),
                        EndTimeVisible = pass.getEndEpoch().toDateTime().ToLocalTime()
                    });
                }
            }
            if (visibilityList.Count > 0)
            {
                dataGridViewSatellites.DataBindingComplete += dataGridViewSatellites_DataBindingComplete;
                dataGridViewSatellites.DataSource = visibilityList;
            }
        }
        private void GetSatelliteInformation()
        {
            DateTime nowUtc = DateTime.Now;
            Coordinate observer = new Coordinate(observerLat, observerLon, observerAlt);
            string[] lines = File.ReadAllLines(tleFilePath);
            List<SatelliteInfo> nextPasses = new List<SatelliteInfo>();

            for (int i = 0; i < lines.Length; i += 3)
            {
                if (i + 2 >= lines.Length) break;
                string name = lines[i].Trim();
                string line1 = lines[i + 1].Trim();
                string line2 = lines[i + 2].Trim();

                Tle tle = ParserTLE.parseTle(line1, line2, name);
                Sgp4 sgp4Propagator = new Sgp4(tle, Sgp4.wgsConstant.WGS_84);
                EpochTime startTimeEpoch = new EpochTime(nowUtc);
                //List<Pass> allPasses = SatFunctions.CalculatePasses(observer, tle, startTimeEpoch, 10, 1, Sgp4.wgsConstant.WGS_84);
                List<Pass> allPasses = SatFunctions.CalculatePasses(observer, tle, startTimeEpoch);
                Pass closestPass = allPasses
                    .Where(pass => pass.getStartEpoch().toDateTime().ToLocalTime() > nowUtc.ToLocalTime())
                    .OrderBy(pass => pass.getStartEpoch().toDateTime().ToLocalTime()).FirstOrDefault();
                if (closestPass != null)
                {
                    DateTime passStartTime = closestPass.getStartEpoch().toDateTime().ToLocalTime();
                    DateTime passEndTime = closestPass.getEndEpoch().toDateTime().ToLocalTime();
                    TimeSpan passDuration = passEndTime - passStartTime;
                    double sampleIntervalSeconds = 1; // Lấy mẫu mỗi 1 giây trong pass
                    /*
                    List<DateTime> trackTimestamps = new List<DateTime>();
                    List<double> azimuthAngles = new List<double>();
                    List<double> elevationAngles = new List<double>();
                    List<double> latitudeAngles = new List<double>();
                    List<double> longitudeAngles = new List<double>();
                    for (double seconds = 0; seconds < passDuration.TotalSeconds; seconds += sampleIntervalSeconds)
                    {
                        DateTime currentTime = passStartTime.AddSeconds(seconds);
                        EpochTime currentEpochTime = new EpochTime(currentTime);
                        Sgp4Data satData = SatFunctions.getSatPositionAtTime(tle, currentEpochTime, Sgp4.wgsConstant.WGS_84);
                        if (satData != null)
                        {
                            Coordinate ground = SatFunctions.calcSatSubPoint(currentEpochTime, satData, Sgp4.wgsConstant.WGS_84);
                            Point3d spherical = SatFunctions.calcSphericalCoordinate(observer, currentEpochTime, satData);
                            if (spherical.z > 3)
                            {
                                trackTimestamps.Add(currentTime);
                                azimuthAngles.Add(spherical.y);
                                elevationAngles.Add(spherical.z);
                                latitudeAngles.Add(ground.getLatitude());
                                longitudeAngles.Add(ground.getLongitude());
                            }
                        }
                    }
                    */
                    sgp4Propagator.runSgp4Cal(closestPass.getStartEpoch(), closestPass.getEndEpoch(), sampleIntervalSeconds / 60.0); // Bước thời gian tính bằng phút
                    List<Sgp4Data> passResultDataList = sgp4Propagator.getResults();

                    if (passResultDataList != null)
                    {
                        List<DateTime> trackTimestamps = new List<DateTime>();
                        List<double> azimuthAngles = new List<double>();
                        List<double> elevationAngles = new List<double>();
                        List<double> latitudeAngles = new List<double>();
                        List<double> longitudeAngles = new List<double>();

                        for (int j = 0; j < passResultDataList.Count; j++)
                        {
                            DateTime currentTime = passStartTime.AddSeconds(j * sampleIntervalSeconds);
                            EpochTime currentEpochTime = new EpochTime(currentTime);
                            Sgp4Data satData = passResultDataList[j];
                            Point3d spherical = SatFunctions.calcSphericalCoordinate(observer, currentEpochTime, satData);
                            Coordinate ground = SatFunctions.calcSatSubPoint(currentEpochTime, satData, Sgp4.wgsConstant.WGS_84);
                            if (spherical.z > 3) // Chỉ lấy các điểm khi vệ tinh nhìn thấy
                            {
                                trackTimestamps.Add(currentTime);
                                azimuthAngles.Add(spherical.y);
                                elevationAngles.Add(spherical.z);
                                latitudeAngles.Add(ground.getLatitude());
                                longitudeAngles.Add(ground.getLongitude());
                            }
                        }

                        nextPasses.Add(new SatelliteInfo
                        {
                            Name = name,
                            StartVisibleTime = passStartTime,
                            EndVisibleTime = passEndTime,
                            TrackTimestamps = trackTimestamps,
                            AzimuthAngles = azimuthAngles,
                            ElevationAngles = elevationAngles,
                            LatitudeAngles = latitudeAngles,
                            LongtitudeAngles = longitudeAngles,
                        });
                    }
                }
            }
            informationList = nextPasses.OrderBy(pass => pass.StartVisibleTime).ToList();

            if (informationList.Count > 0)
            {
                dataGridInforSatellites.DataBindingComplete += dataGridInforSatellites_DataBindingComplete;
                dataGridInforSatellites.DataSource = informationList;
            }
        }
        private void dataGridInforSatellites_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridInforSatellites.SelectedRows.Count == 0) return;

            selectedSatellite = (SatelliteInfo)dataGridInforSatellites.SelectedRows[0].DataBoundItem;
            if (selectedSatellite == null || !selectedSatellite.StartVisibleTime.HasValue) return;

            DialogResult result = MessageBox.Show(
                $"Bạn có muốn theo dõi vệ tinh '{selectedSatellite.Name}' không?",
                "Xác nhận theo dõi",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (result != DialogResult.OK)
            {
                SetTrackingInfo("Đã hủy chọn vệ tinh theo dõi.");
                return;
            }

            ClearPendingTracking();
            StopAntennaTrajectoryTracking();

            ClearAllLines();
            DrawLineFromArrays(selectedSatellite.LatitudeAngles,
                               selectedSatellite.LongtitudeAngles,
                               Color.Red, 3, "");
            DisplaySatelliteAtTimestamp();
            DrawElevationChart(selectedSatellite.TrackTimestamps,
                               selectedSatellite.ElevationAngles,
                               elevationAnglesRecei,
                               selectedSatellite.Name);
            DrawAzimuthChart(selectedSatellite.TrackTimestamps,
                             selectedSatellite.AzimuthAngles,
                             selectedSatellite.Name);

            timeStampsSend = selectedSatellite.TrackTimestamps;
            azimuthAnglesSend = selectedSatellite.AzimuthAngles;
            elevationAnglesSend = selectedSatellite.ElevationAngles;
            SolarAzimuthSend = selectedSatellite.SolarAzimuth;
            SolarelEvationSend = selectedSatellite.SolarElevation;
            RelativeAzimuthSend = selectedSatellite.RelativeAzimuth;
            RelativeElevationSend = selectedSatellite.RelativeElevation;

            List<string> timeStrings = timeStampsSend.ConvertAll(dt => dt.ToString("HH:mm:ss"));
            List<float> floatListAzi = azimuthAnglesSend.ConvertAll(x => (float)(int)(x * 1000.0));
            List<float> floatListEle = elevationAnglesSend.ConvertAll(y => (float)(int)(y * 1000.0));
            List<float> floatListSolarAzi = SolarAzimuthSend.ConvertAll(x => (float)(int)(x * 1000.0));
            List<float> floatListSolarEle = SolarelEvationSend.ConvertAll(y => (float)(int)(y * 1000.0));
            List<float> floatListRelativeAzi = RelativeAzimuthSend.ConvertAll(x => (float)(int)(x * 1000.0));
            List<float> floatListRelativeEle = RelativeElevationSend.ConvertAll(y => (float)(int)(y * 1000.0));
            File.WriteAllLines(RuntimeDataPath("timeStrings.txt"), timeStrings);
            File.WriteAllLines(RuntimeDataPath("floatListAzi.txt"), floatListAzi.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            File.WriteAllLines(RuntimeDataPath("floatListEle.txt"), floatListEle.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            File.WriteAllLines(RuntimeDataPath("floatListSolarAzi.txt"), floatListSolarAzi.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            File.WriteAllLines(RuntimeDataPath("floatListSolarEle.txt"), floatListSolarEle.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            File.WriteAllLines(RuntimeDataPath("floatListRelativeAzi.txt"), floatListRelativeAzi.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            File.WriteAllLines(RuntimeDataPath("floatListRelativeEle.txt"), floatListRelativeEle.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            indexSend = 0; indexRecei = 0;

            _pendingTrackingStartTime = timeStampsSend.Count > 0 ? timeStampsSend[0] : selectedSatellite.StartVisibleTime;
            _pendingTrackingSatelliteName = selectedSatellite.Name;
            ApplyControlMode(ControlMode.Manual);
            SetTrackingInfo($"Đã xếp '{selectedSatellite.Name}' vào hàng chờ. Nhấn TRACKING để vào chế độ tự động.");
        }

        private void StartAntennaTrajectoryTracking()
        {
            StopAntennaTrajectoryTracking();

            var loadStatus = LoadTrackingTrajectoryFromRuntimeFiles(
                _activeTrackingTarget,
                out var errorMessage,
                out var startTime,
                out var endTime);

            if (loadStatus == TrackingDataLoadStatus.Pending)
            {
                QueuePendingTracking(startTime);
                return;
            }

            if (loadStatus != TrackingDataLoadStatus.Ready)
            {
                SetTrackingInfo(errorMessage);
                if (loadStatus == TrackingDataLoadStatus.Invalid || loadStatus == TrackingDataLoadStatus.Expired)
                {
                    MessageBox.Show(errorMessage, "Tracking",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ApplyControlMode(ControlMode.Manual);
                }
                return;
            }

            if (timeStampsSend == null || azimuthAnglesSend == null || elevationAnglesSend == null ||
                timeStampsSend.Count == 0 ||
                timeStampsSend.Count != azimuthAnglesSend.Count ||
                timeStampsSend.Count != elevationAnglesSend.Count)
            {
                string targetName = GetTrackingTargetName(_activeTrackingTarget);
                SetTrackingInfo($"Dữ liệu tracking {targetName} không hợp lệ.");
                MessageBox.Show($"Dữ liệu tracking {targetName} không hợp lệ.", "Tracking",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ApplyControlMode(ControlMode.Manual);
                return;
            }

            if (!IsServoReady(Manual_Control.Axis.Azimuth) || !IsServoReady(Manual_Control.Axis.Elevation))
            {
                string targetName = GetTrackingTargetName(_activeTrackingTarget);
                SetTrackingInfo($"Cần Connect và Enable cả 2 servo trước khi tracking {targetName}.");
                MessageBox.Show($"Cần Connect và Enable cả 2 servo trước khi tracking {targetName}.", "Tracking",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ApplyControlMode(ControlMode.Manual);
                return;
            }

            if (!IsEncoderReady(Manual_Control.Axis.Azimuth) || !IsEncoderReady(Manual_Control.Axis.Elevation))
            {
                string targetName = GetTrackingTargetName(_activeTrackingTarget);
                SetTrackingInfo($"Cần Encoder Azimuth/Elevation có dữ liệu hợp lệ trước khi tracking {targetName}.");
                MessageBox.Show($"Cần Encoder Azimuth/Elevation có dữ liệu hợp lệ trước khi tracking {targetName}.", "Tracking",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ApplyControlMode(ControlMode.Manual);
                return;
            }

            ClearPendingTracking();
            _trackingPausedBelowElevationLimit = false;
            SetTrackingInfo($"Đang tracking {GetTrackingTargetName(_activeTrackingTarget)} từ {startTime:HH:mm:ss} đến {endTime:HH:mm:ss}.");

            _antennaTrackingTimer = new System.Windows.Forms.Timer
            {
                Interval = Math.Max(50, GetIntSetting("TrackingIntervalMs", 200))
            };
            _antennaTrackingTimer.Tick += AntennaTrackingTimer_Tick;
            _antennaTrackingTimer.Start();
            AntennaTrackingTimer_Tick(_antennaTrackingTimer, EventArgs.Empty);
        }

        private void QueuePendingTracking(DateTime startTime)
        {
            _pendingTrackingStartTime = startTime;
            _pendingTrackingSatelliteName = selectedSatellite?.Name;

            _pendingTrackingTimer ??= new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            _pendingTrackingTimer.Tick -= PendingTrackingTimer_Tick;
            _pendingTrackingTimer.Tick += PendingTrackingTimer_Tick;
            _pendingTrackingTimer.Start();

            PendingTrackingTimer_Tick(_pendingTrackingTimer, EventArgs.Empty);
        }

        private async void PendingTrackingTimer_Tick(object? sender, EventArgs e)
        {
            if (_controlMode != ControlMode.Tracking)
            {
                ClearPendingTracking();
                return;
            }

            if (!_pendingTrackingStartTime.HasValue)
            {
                ClearPendingTracking();
                return;
            }

            if (DateTime.Now >= _pendingTrackingStartTime.Value)
            {
                ClearPendingTracking(keepInfo: true);
                SetTrackingInfo("Đã đến thời gian theo dõi. Đang khởi động tracking...");
                StartAntennaTrajectoryTracking();
                return;
            }

            await PrepositionAntennaToTrackingStartAsync();
        }

        private async Task PrepositionAntennaToTrackingStartAsync()
        {
            if (_pendingPrepositionBusy || _manual == null ||
                timeStampsSend == null || azimuthAnglesSend == null || elevationAnglesSend == null ||
                timeStampsSend.Count == 0 || azimuthAnglesSend.Count == 0 || elevationAnglesSend.Count == 0)
            {
                UpdatePendingTrackingInfo();
                return;
            }

            _pendingPrepositionBusy = true;
            try
            {
                float targetAzi = (float)azimuthAnglesSend[0];
                float targetEle = (float)elevationAnglesSend[0];
                float minElevationDeg = GetFloatSetting("TrackingMinElevationDeg", 10f);

                if (targetEle < minElevationDeg)
                {
                    _ = _manual.StopManualMoveAsync(Manual_Control.Axis.Azimuth);
                    _ = _manual.StopManualMoveAsync(Manual_Control.Axis.Elevation);
                    UpdatePendingTrackingInfo(
                        $"Đang chờ, chưa quay anten vì Elevation điểm đầu {targetEle:F2}° nhỏ hơn ngưỡng {minElevationDeg:F2}°.");
                    return;
                }

                if (!IsServoReady(Manual_Control.Axis.Azimuth) || !IsServoReady(Manual_Control.Axis.Elevation))
                {
                    UpdatePendingTrackingInfo("Cần Connect và Enable cả 2 servo để đưa anten tới điểm đầu track.");
                    return;
                }

                if (!IsEncoderReady(Manual_Control.Axis.Azimuth) || !IsEncoderReady(Manual_Control.Axis.Elevation))
                {
                    UpdatePendingTrackingInfo("Cần Encoder Azimuth/Elevation có dữ liệu hợp lệ để đưa anten tới điểm đầu track.");
                    return;
                }

                float toleranceDeg = Math.Max(0.01f, GetFloatSetting("TrackingToleranceDeg", 0.2f));

                await Task.WhenAll(
                    _manual.TrackAxisToTargetAsync(Manual_Control.Axis.Azimuth, targetAzi, toleranceDeg),
                    _manual.TrackAxisToTargetAsync(Manual_Control.Axis.Elevation, targetEle, toleranceDeg));

                UpdatePendingTrackingInfo(
                    $"Đang đưa anten tới điểm đầu track để chờ sẵn: Azi {targetAzi:F2}°, Ele {targetEle:F2}°.");
            }
            catch (Exception ex)
            {
                UpdatePendingTrackingInfo($"Lỗi đưa anten tới điểm đầu track: {ex.Message}");
            }
            finally
            {
                _pendingPrepositionBusy = false;
            }
        }

        private void UpdatePendingTrackingInfo(string extraMessage = "")
        {
            if (!_pendingTrackingStartTime.HasValue) return;

            TimeSpan wait = _pendingTrackingStartTime.Value - DateTime.Now;
            if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;

            string satelliteName = string.IsNullOrWhiteSpace(_pendingTrackingSatelliteName)
                ? "vệ tinh đã chọn"
                : _pendingTrackingSatelliteName;

            string message =
                $"Đã đưa '{satelliteName}' vào hàng chờ tracking {GetTrackingTargetName(_activeTrackingTarget)}. Bắt đầu lúc {_pendingTrackingStartTime.Value:HH:mm:ss}, còn {wait:hh\\:mm\\:ss}.";

            if (!string.IsNullOrWhiteSpace(extraMessage))
            {
                message += $" {extraMessage}";
            }

            SetTrackingInfo(message);
        }

        private void ClearPendingTracking(bool keepInfo = false)
        {
            if (_pendingTrackingTimer != null)
            {
                _pendingTrackingTimer.Stop();
                _pendingTrackingTimer.Tick -= PendingTrackingTimer_Tick;
            }

            _pendingTrackingStartTime = null;
            _pendingTrackingSatelliteName = null;
            _pendingPrepositionBusy = false;

            if (!keepInfo && lblSatus != null)
            {
                SetTrackingInfo(string.Empty);
            }
        }

        private TrackingDataLoadStatus LoadTrackingTrajectoryFromRuntimeFiles(
            TrackingTarget target,
            out string errorMessage,
            out DateTime startTime,
            out DateTime endTime)
        {
            errorMessage = string.Empty;
            startTime = DateTime.MinValue;
            endTime = DateTime.MinValue;

            string timePath = RuntimeDataPath("timeStrings.txt");
            string aziPath = RuntimeDataPath(target == TrackingTarget.Sun
                ? "floatListSolarAzi.txt"
                : "floatListAzi.txt");
            string elePath = RuntimeDataPath(target == TrackingTarget.Sun
                ? "floatListSolarEle.txt"
                : "floatListEle.txt");
            string targetName = GetTrackingTargetName(target);

            if (!File.Exists(timePath) || !File.Exists(aziPath) || !File.Exists(elePath))
            {
                errorMessage =
                    $"Chưa có đủ file dữ liệu tracking {targetName}. Hãy chọn một vệ tinh trong dataGridInforSatellites trước.";
                return TrackingDataLoadStatus.Invalid;
            }

            try
            {
                string[] timeLines = File.ReadAllLines(timePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();
                string[] aziLines = File.ReadAllLines(aziPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();
                string[] eleLines = File.ReadAllLines(elePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();

                if (timeLines.Length == 0 ||
                    timeLines.Length != aziLines.Length ||
                    timeLines.Length != eleLines.Length)
                {
                    errorMessage = $"Dữ liệu tracking {targetName} trong file không hợp lệ hoặc số dòng không khớp.";
                    return TrackingDataLoadStatus.Invalid;
                }

                DateTime now = DateTime.Now;
                List<DateTime> timestamps = BuildTrackingTimestamps(timeLines, now);
                List<double> aziValues = ParseScaledAngleLines(aziLines);
                List<double> eleValues = ParseScaledAngleLines(eleLines);

                if (timestamps.Count == 0 ||
                    timestamps.Count != aziValues.Count ||
                    timestamps.Count != eleValues.Count)
                {
                    errorMessage = $"Không đọc được đầy đủ dữ liệu thời gian/góc tracking {targetName} từ file.";
                    return TrackingDataLoadStatus.Invalid;
                }

                startTime = timestamps[0];
                endTime = timestamps[^1];

                if (now < startTime)
                {
                    errorMessage =
                        $"Chưa tới thời gian theo dõi. Thời gian bắt đầu: {startTime:yyyy-MM-dd HH:mm:ss}, hiện tại: {now:yyyy-MM-dd HH:mm:ss}.";
                    timeStampsSend = timestamps;
                    azimuthAnglesSend = aziValues;
                    elevationAnglesSend = eleValues;
                    return TrackingDataLoadStatus.Pending;
                }

                if (now > endTime)
                {
                    errorMessage =
                        $"Đã quá thời gian theo dõi. Thời gian kết thúc: {endTime:yyyy-MM-dd HH:mm:ss}, hiện tại: {now:yyyy-MM-dd HH:mm:ss}.";
                    return TrackingDataLoadStatus.Expired;
                }

                timeStampsSend = timestamps;
                azimuthAnglesSend = aziValues;
                elevationAnglesSend = eleValues;
                return TrackingDataLoadStatus.Ready;
            }
            catch (Exception ex)
            {
                errorMessage = $"Không đọc được file dữ liệu tracking {GetTrackingTargetName(target)}: {ex.Message}";
                return TrackingDataLoadStatus.Invalid;
            }
        }

        private static string GetTrackingTargetName(TrackingTarget target)
        {
            return target == TrackingTarget.Sun ? "Mặt Trời" : "vệ tinh";
        }

        private static List<DateTime> BuildTrackingTimestamps(string[] timeLines, DateTime now)
        {
            var candidates = new[]
            {
                BuildTrackingTimestampsForDate(timeLines, now.Date.AddDays(-1)),
                BuildTrackingTimestampsForDate(timeLines, now.Date),
                BuildTrackingTimestampsForDate(timeLines, now.Date.AddDays(1))
            };

            foreach (var candidate in candidates)
            {
                if (candidate.Count > 0 && now >= candidate[0] && now <= candidate[^1])
                {
                    return candidate;
                }
            }

            return candidates
                .Where(candidate => candidate.Count > 0)
                .OrderBy(candidate => GetDistanceToInterval(now, candidate[0], candidate[^1]))
                .FirstOrDefault() ?? new List<DateTime>();
        }

        private static List<DateTime> BuildTrackingTimestampsForDate(string[] timeLines, DateTime baseDate)
        {
            var result = new List<DateTime>();
            int dayOffset = 0;
            TimeSpan? previousTime = null;

            foreach (string line in timeLines)
            {
                if (!TimeSpan.TryParseExact(line.Trim(), @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var timeOfDay) &&
                    !TimeSpan.TryParse(line.Trim(), CultureInfo.InvariantCulture, out timeOfDay))
                {
                    throw new FormatException($"Thời gian không hợp lệ: {line}");
                }

                if (previousTime.HasValue && timeOfDay < previousTime.Value)
                {
                    dayOffset++;
                }

                result.Add(baseDate.AddDays(dayOffset).Add(timeOfDay));
                previousTime = timeOfDay;
            }

            return result;
        }

        private static double GetDistanceToInterval(DateTime value, DateTime start, DateTime end)
        {
            if (value < start) return (start - value).TotalSeconds;
            if (value > end) return (value - end).TotalSeconds;
            return 0;
        }

        private static List<double> ParseScaledAngleLines(string[] lines)
        {
            var rawValues = new List<double>(lines.Length);
            foreach (string line in lines)
            {
                if (!double.TryParse(line.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    throw new FormatException($"Góc không hợp lệ: {line}");
                }

                rawValues.Add(value);
            }

            bool valuesAreScaled = rawValues.Any(value => Math.Abs(value) > 360.0);
            return valuesAreScaled
                ? rawValues.Select(value => value / 1000.0).ToList()
                : rawValues;
        }

        private async void AntennaTrackingTimer_Tick(object? sender, EventArgs e)
        {
            if (_antennaTrackingBusy || _manual == null ||
                timeStampsSend == null || azimuthAnglesSend == null || elevationAnglesSend == null)
            {
                return;
            }

            _antennaTrackingBusy = true;
            try
            {
                DateTime now = DateTime.Now;
                DateTime endTime = timeStampsSend[^1];

                if (now > endTime)
                {
                    StopAntennaTrajectoryTracking();
                    ClearAllLines();
                    ApplyControlMode(ControlMode.Manual);
                    SetTrackingInfo("Tracking completed. Manual mode.");
                    return;
                }

                float targetAzi = (float)GetTrajectoryValueAtTime(timeStampsSend, azimuthAnglesSend, now);
                float targetEle = (float)GetTrajectoryValueAtTime(timeStampsSend, elevationAnglesSend, now);
                float minElevationDeg = GetFloatSetting("TrackingMinElevationDeg", 10f);

                if (targetEle < minElevationDeg)
                {
                    if (!_trackingPausedBelowElevationLimit)
                    {
                        _ = _manual.StopManualMoveAsync(Manual_Control.Axis.Azimuth);
                        _ = _manual.StopManualMoveAsync(Manual_Control.Axis.Elevation);
                        _trackingPausedBelowElevationLimit = true;
                    }

                    SetTrackingInfo(
                        $"Tạm dừng tracking {GetTrackingTargetName(_activeTrackingTarget)}: Elevation mục tiêu {targetEle:F2}° nhỏ hơn ngưỡng {minElevationDeg:F2}°.");
                    return;
                }

                if (_trackingPausedBelowElevationLimit)
                {
                    _trackingPausedBelowElevationLimit = false;
                    SetTrackingInfo(
                        $"Tiếp tục tracking {GetTrackingTargetName(_activeTrackingTarget)}: Elevation mục tiêu {targetEle:F2}° đã đạt ngưỡng {minElevationDeg:F2}°.");
                }

                float toleranceDeg = Math.Max(0.01f, GetFloatSetting("TrackingToleranceDeg", 0.2f));

                await Task.WhenAll(
                    _manual.TrackAxisToTargetAsync(Manual_Control.Axis.Azimuth, targetAzi, toleranceDeg),
                    _manual.TrackAxisToTargetAsync(Manual_Control.Axis.Elevation, targetEle, toleranceDeg));
            }
            catch (Exception ex)
            {
                StopAntennaTrajectoryTracking();
                MessageBox.Show($"Loi tracking anten: {ex.Message}", "Tracking",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _antennaTrackingBusy = false;
            }
        }

        private void StopAntennaTrajectoryTracking()
        {
            _trackingPausedBelowElevationLimit = false;

            if (_antennaTrackingTimer != null)
            {
                _antennaTrackingTimer.Stop();
                _antennaTrackingTimer.Tick -= AntennaTrackingTimer_Tick;
                _antennaTrackingTimer.Dispose();
                _antennaTrackingTimer = null;
            }

            _ = _manual?.StopManualMoveAsync(Manual_Control.Axis.Azimuth);
            _ = _manual?.StopManualMoveAsync(Manual_Control.Axis.Elevation);
        }

        private static double GetTrajectoryValueAtTime(List<DateTime> timestamps, List<double> values, DateTime now)
        {
            if (timestamps.Count == 0 || values.Count == 0) return 0;
            if (now <= timestamps[0]) return values[0];

            int last = Math.Min(timestamps.Count, values.Count) - 1;
            if (now >= timestamps[last]) return values[last];

            for (int i = 1; i <= last; i++)
            {
                if (now > timestamps[i]) continue;

                double totalMs = (timestamps[i] - timestamps[i - 1]).TotalMilliseconds;
                if (totalMs <= 0) return values[i];

                double ratio = (now - timestamps[i - 1]).TotalMilliseconds / totalMs;
                return values[i - 1] + ((values[i] - values[i - 1]) * ratio);
            }

            return values[last];
        }
        private void OnInitialTimerElapsed(object sender, ElapsedEventArgs e)
        {
            SendInitialData(null);
            if (sender is System.Timers.Timer timer)
            {
                timer.Dispose();
            }
        }
        private void StartSendingData()
        {
            if (timeStampsSend == null || azimuthAnglesSend == null || elevationAnglesSend == null ||
                timeStampsSend.Count == 0 || timeStampsSend.Count != azimuthAnglesSend.Count ||
                timeStampsSend.Count != elevationAnglesSend.Count)
            {
                return;
            }
            if (timeStampsSend.Count > 0)
            {
                /*
                List<string> timeStrings = timeStampsSend.ConvertAll(dt => dt.ToString("HH:mm:ss"));
                List<float> floatListAzi = azimuthAnglesSend.ConvertAll(x => (float)(int)(x * 1000.0));
                List<float> floatListEle = elevationAnglesSend.ConvertAll(y => (float)(int)(y * 1000.0));
                for (int i = 0; i < timeStrings.Count; i++)
                {
                    // Nối các phần tử tại cùng chỉ số `i` với nhau, ngăn cách bằng dấu phẩy ","
                    resultBuilder.Append($"{timeStrings[i]},{floatListAzi[i]},{floatListEle[i]}");
                    // Nếu đây không phải là phần tử cuối cùng, thêm dấu chấm phẩy ";"
                    if (i < timeStrings.Count - 1)
                    {
                        resultBuilder.Append(";");
                    }
                }
                */
                DateTime firstTimestamp = timeStampsSend[0];
                DateTime triggerTime = firstTimestamp.AddSeconds(-45);
                TimeSpan delay = triggerTime - DateTime.Now;

                if (delay > TimeSpan.Zero)
                {
                    System.Timers.Timer initialTimer = new System.Timers.Timer(delay.TotalMilliseconds);
                    initialTimer.AutoReset = false;
                    initialTimer.Elapsed += OnInitialTimerElapsed;
                    initialTimer.Start();
                }
                else
                {
                    SendInitialData(null);
                }
            }
            /*
            if (timeStampsSend.Count > 0)
            {
                for (int i = 0; i < timeStampsSend.Count; i++)
                {
                    DateTime timestamp = timeStampsSend[i];
                    indexSend = i;
                    TimeSpan delay = timestamp - DateTime.Now;
                    if (delay > TimeSpan.Zero)
                    {
                        System.Timers.Timer timestampTimer = new System.Timers.Timer(delay.TotalMilliseconds);
                        timestampTimer.AutoReset = false;
                        timestampTimer.Elapsed += (sender, e) => SendDataAtTimestamp(indexSend);
                        timestampTimer.Start();
                    }
                    else if (delay <= TimeSpan.Zero && delay > TimeSpan.FromSeconds(-1))
                    {
                        SendDataAtTimestamp(indexSend);
                    }
                }
            }
            */
        }
        private async Task ReceiveAndProcessResponse()
        {
            try
            {
                udpClientReceive.Client.ReceiveBufferSize = 65536;
                udpClientReceive.Client.ReceiveTimeout = 100; // Đặt thời gian chờ nhận dữ liệu là 1/10 giây
                IPEndPoint remoteEP = null;
                byte[] receivedBytes = udpClientReceive.Receive(ref remoteEP);

                if (receivedBytes != null && receivedBytes.Length == 4)
                {
                    if (receivedBytes[0] == 0xFE && receivedBytes[1] == 0xFE && receivedBytes[2] == 0xFA && receivedBytes[3] == 0xFD)
                    {
                        MessageBox.Show("Gửi thành công");
                    }
                    else if (receivedBytes[0] == 0xFE && receivedBytes[1] == 0xFE && receivedBytes[2] == 0xFB && receivedBytes[3] == 0xFD)
                    {
                        MessageBox.Show("Gửi không thành công");
                        /*
                        List<string> timeStrings = timeStampsSend.ConvertAll(dt => dt.ToString("HH:mm:ss"));
                        List<float> floatListAzi = azimuthAnglesSend.ConvertAll(x => (float)(int)(x * 1000.0));
                        List<float> floatListEle = elevationAnglesSend.ConvertAll(y => (float)(int)(y * 1000.0));
                        SendStringToUdp(string.Join(",", timeStrings));
                        SendStringToUdp(string.Join(",", floatListAzi));
                        SendStringToUdp(string.Join(",", floatListEle));
                        */
                    }
                }
                if (receivedBytes != null && receivedBytes.Length == 8)
                {
                    azimuthAnglesRecei[indexRecei] = BitConverter.ToSingle(receivedBytes, 0) / 1000.0;
                    elevationAnglesRecei[indexRecei] = BitConverter.ToSingle(receivedBytes, 4) / 1000.0;
                    indexRecei++;
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    //MessageBox.Show("Đã xảy ra lỗi hết thời gian chờ nhận dữ liệu.");
                }
                else
                {
                    //MessageBox.Show($"Lỗi khi nhận phản hồi UDP: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Đã xảy ra lỗi không mong muốn khi nhận dữ liệu UDP: {ex.Message}");
            }
        }
        private async Task Send2FloadToUdp()
        {
            using (UdpClient udpClientSend = new UdpClient())
            {
                try
                {
                    udpClientSend.Client.SendBufferSize = 65536;
                    udpClientSend.Client.ReceiveBufferSize = 65536;
                    IPEndPoint sendEndPoint = new IPEndPoint(IPAddress.Parse(udpAddress), sendPort);
                    byte[] dataToSend = TwoScaledFloadToBytes((float)currentAzimuth, (float)currentElevation);
                    await udpClientSend.SendAsync(dataToSend, dataToSend.Length, sendEndPoint);
                    await Task.Delay(5); // Thêm độ trễ 10ms giữa các lần gửi 
                    await ReceiveAndProcessResponse();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi gửi UDP: {ex.Message}");
                }
            }
        }
        private async Task SendStringToUdp(string stringSend)
        {
            using (UdpClient udpClientSend = new UdpClient())
            {
                try
                {
                    udpClientSend.Client.SendBufferSize = 65536;
                    udpClientSend.Client.ReceiveBufferSize = 65536;
                    IPEndPoint sendEndPoint = new IPEndPoint(IPAddress.Parse(udpAddress), sendPort);
                    byte[] dataToSend = Encoding.ASCII.GetBytes(stringSend);
                    await udpClientSend.SendAsync(dataToSend, dataToSend.Length, sendEndPoint);
                    await Task.Delay(5); // Thêm độ trễ 10ms giữa các lần gửi 
                    await ReceiveAndProcessResponse();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi gửi UDP: {ex.Message}");
                }
            }
        }
        private void SendInitialData(object state)
        {
            if (azimuthAnglesSend.Count > 0 && elevationAnglesSend.Count > 0)
            {
                //currentAzimuth = azimuthAnglesSend[0];
                //currentElevation = elevationAnglesSend[0];
                //SendUdpMessage();
                List<string> timeStrings = timeStampsSend.ConvertAll(dt => dt.ToString("HH:mm:ss"));
                List<float> floatListAzi = azimuthAnglesSend.ConvertAll(x => (float)(int)(x * 1000.0));
                List<float> floatListEle = elevationAnglesSend.ConvertAll(y => (float)(int)(y * 1000.0));
                File.WriteAllLines(RuntimeDataPath("timeStrings.txt"), timeStrings);
                File.WriteAllLines(RuntimeDataPath("floatListAzi.txt"), floatListAzi.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                File.WriteAllLines(RuntimeDataPath("floatListEle.txt"), floatListEle.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                SendStringToUdp(string.Join(",", timeStrings));
                SendStringToUdp(string.Join(",", floatListAzi));
                SendStringToUdp(string.Join(",", floatListEle));
            }
        }
        private void SendDataAtTimestamp(object indexObj)
        {
            int index = (int)indexObj;
            if (index >= 0 && index < azimuthAnglesSend.Count && index < elevationAnglesSend.Count)
            {
                currentAzimuth = azimuthAnglesSend[index];
                currentElevation = elevationAnglesSend[index];
                MessageBox.Show($"gửi UDP gói " + index.ToString() + ": Azi := " + currentAzimuth.ToString() + "; Ele:= " + currentElevation.ToString());
                Send2FloadToUdp();
            }
        }
        private void DrawSatellitePathOnGMap(SatelliteInfo satellite)
        {
            if (satellite.LatitudeAngles.Count == 0 || satellite.LongtitudeAngles.Count == 0)
            {
                return;
            }

            for (int i = 0; i < satellite.LatitudeAngles.Count; i++)
            {
                PointLatLng point = new PointLatLng(satellite.LatitudeAngles[i], satellite.LongtitudeAngles[i]);
                // Create a Google marker (you can choose other marker types)
                GMarkerGoogle marker = new GMarkerGoogle(point, GMarkerGoogleType.red_dot); // You can change the color/type
                marker.ToolTipText = satellite.Name + " (" + point.Lat.ToString("F6") + ", " + point.Lng.ToString("F6") + ")";
                satOverlay.Markers.Add(marker);
            }
            // Clear any existing overlays and add the new one
            gMap.Overlays.Clear();
            gMap.Overlays.Add(satOverlay);
        }
        private void SatelliteAtTimestamp(object indexObj)
        {
            int index = (int)indexObj;
            if (index >= 0 && index < selectedSatellite.LatitudeAngles.Count && index < selectedSatellite.LongtitudeAngles.Count)
            {
                double currentLat = selectedSatellite.LatitudeAngles[index];
                double currentLong = selectedSatellite.LongtitudeAngles[index];
                //AddPoint(currentLat, currentLong, $"{selectedSatellite}\nTime: {selectedSatellite.TrackTimestamps[index].ToString("mm:ss")}\nLat: {currentLat}\nLon: {currentLong}", Color.Blue);
                AddPoint(selectedSatellite.LatitudeAngles[index], selectedSatellite.LongtitudeAngles[index], selectedSatellite.Name, Color.Blue, true, sizeInCm: 1);
            }
        }
        private void DisplaySatelliteAtTimestamp()
        {
            for (int i = 0; i < selectedSatellite.TrackTimestamps.Count; i++)
            {
                DateTime timestamp = selectedSatellite.TrackTimestamps[i];
                int index = i;
                TimeSpan delay = timestamp - DateTime.Now;
                if (delay > TimeSpan.Zero)
                {
                    System.Timers.Timer timestampTimer = new System.Timers.Timer(delay.TotalMilliseconds);
                    timestampTimer.AutoReset = false;
                    timestampTimer.Elapsed += (sender, e) => SatelliteAtTimestamp(index);
                    timestampTimer.Start();
                }
                else if (delay <= TimeSpan.Zero && delay > TimeSpan.FromSeconds(-1))
                {
                    SatelliteAtTimestamp(index);
                }
            }
        }
        private static bool IsRunAsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        private void MainTimer_Tick(object sender, EventArgs e)
        {
            //GetSatelliteInformation();
            LoadSatelliteData(); // Cập nhật dữ liệu mỗi 15 phút
        }
        private void btnZoomIn_Click(object sender, EventArgs e)
        {
            if (gMap.Zoom < gMap.MaxZoom) gMap.Zoom += 1;
        }
        private void btnZoomOut_Click(object sender, EventArgs e)
        {
            if (gMap.Zoom > gMap.MinZoom) gMap.Zoom -= 1;
        }
        private void btnCenter_Click(object sender, EventArgs e)
        {
            gMap.Position = new GMap.NET.PointLatLng(observerLat, observerLon);
        }
        private void DrawElevationChart(List<DateTime> timestamps, List<double> elevationAngles1, double[] elevationAngles2, string Name = "")
        {
            var model = new PlotModel { Title = $"Biểu đồ góc ngẩng vệ tinh {selectedSatellite.Name}", TitleFontSize = 14 };
            DateTime startTime = timestamps != null && timestamps.Count > 0 ? timestamps[0] : DateTime.Now;
            DateTime endTime = timestamps != null && timestamps.Count > 1 ? timestamps[^1] : startTime.AddSeconds(1);

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm",
                Title = "Thời gian",
                IntervalType = DateTimeIntervalType.Auto,
                Minimum = DateTimeAxis.ToDouble(startTime),
                Maximum = DateTimeAxis.ToDouble(endTime),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };

            var elevationAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Góc ngẩng (°)",
                Minimum = 0,
                Maximum = 90,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };

            model.Axes.Add(timeAxis);
            model.Axes.Add(elevationAxis);

            if (elevationAngles1 != null && elevationAngles1.Count > 0)
            {
                var lineSeries1 = new LineSeries
                {
                    Title = "Góc ngẩng",
                    StrokeThickness = 2,
                    Color = OxyColors.Red,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 1,
                    //MarkerStroke = OxyColors.Black
                };
                int count = Math.Min(timestamps.Count, elevationAngles1.Count);
                for (int i = 0; i < count; i++)
                {
                    lineSeries1.Points.Add(new DataPoint(DateTimeAxis.ToDouble(timestamps[i]), elevationAngles1[i]));
                }
                model.Series.Add(lineSeries1);
            }
            if (elevationAngles2 != null && elevationAngles2.Length > 0)
            {
                var lineSeries2 = new LineSeries
                {
                    Title = "Góc ngẩng",
                    StrokeThickness = 2,
                    Color = OxyColors.Blue,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 1,
                    //MarkerStroke = OxyColors.Black
                };
                int count = Math.Min(timestamps.Count, elevationAngles2.Length);
                for (int i = 0; i < count; i++)
                {
                    lineSeries2.Points.Add(new DataPoint(DateTimeAxis.ToDouble(timestamps[i]), elevationAngles2[i]));
                }
                model.Series.Add(lineSeries2);
            }
            plotView1.Model = model;
        }

        private void DrawAzimuthChart(List<DateTime> timestamps, List<double> azimuthAngles, string Name = "")
        {
            var model = new PlotModel { Title = $"Biểu đồ góc phương vị vệ tinh {selectedSatellite.Name}", TitleFontSize = 14 };
            DateTime startTime = timestamps != null && timestamps.Count > 0 ? timestamps[0] : DateTime.Now;
            DateTime endTime = timestamps != null && timestamps.Count > 1 ? timestamps[^1] : startTime.AddSeconds(1);

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm",
                Title = "Thời gian",
                IntervalType = DateTimeIntervalType.Auto,
                Minimum = DateTimeAxis.ToDouble(startTime),
                Maximum = DateTimeAxis.ToDouble(endTime),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };

            var azimuthAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Góc phương vị (°)",
                Minimum = 0,
                Maximum = 360,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };

            model.Axes.Add(timeAxis);
            model.Axes.Add(azimuthAxis);

            if (timestamps != null && azimuthAngles != null && azimuthAngles.Count > 0)
            {
                var lineSeries = new LineSeries
                {
                    Title = "Góc phương vị",
                    StrokeThickness = 2,
                    Color = OxyColors.DarkGreen,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 1
                };

                int count = Math.Min(timestamps.Count, azimuthAngles.Count);
                for (int i = 0; i < count; i++)
                {
                    lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(timestamps[i]), azimuthAngles[i]));
                }

                model.Series.Add(lineSeries);
            }

            plotView3.Model = model;
        }
        private void btnFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "TLE Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                tleFilePath = openFileDialog1.FileName;
                SetTleFilePath(tleFilePath);
                //LoadSatelliteData(); // Tải lại dữ liệu với file TLE mới
                GetSatelliteInformation();
                GetSatelliteVisibility();
            }
        }
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (selectedSatellite != null)
            {
                if (e.KeyCode == Keys.Up)
                {
                    currentElevation += elevationStep;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    currentElevation -= elevationStep;
                }
                else if (e.KeyCode == Keys.Left)
                {
                    currentAzimuth -= azimuthStep;
                }
                else if (e.KeyCode == Keys.Right)
                {
                    currentAzimuth += azimuthStep;
                }
            }
        }
        private void EleUp_Click(object sender, EventArgs e)
        {
            currentElevation += elevationStep;
        }
        private void EleDown_Click(object sender, EventArgs e)
        {
            currentElevation -= elevationStep;
        }
        private void AziLeft_Click(object sender, EventArgs e)
        {
            currentAzimuth -= azimuthStep;
        }
        private void AziRight_Click(object sender, EventArgs e)
        {
            currentAzimuth += azimuthStep;
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void label10_Click(object sender, EventArgs e)
        {

        }

        private void glassButton1_Click(object sender, EventArgs e)
        {
            btnSunTracking_Click(sender, e);
        }
    }
}
