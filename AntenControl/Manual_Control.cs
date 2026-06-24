using System;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using Kinco;
using Modbus;

namespace AntenControl
{
    public sealed class Manual_Control : IDisposable
    {
        public enum Axis { Azimuth, Elevation }

        // ===== One channel UI pack (passed from MainForm) =====
        public sealed class AxisUi
        {
            public required Button BtnConnect { get; init; }
            public required Button BtnEnable { get; init; }

            public required TextBox TxbSpeedRpm { get; init; }     // RPM

            public required TextBox TxbPositionDeg { get; init; }  // Absolute position from encoder extension
            public required Label LblStatus { get; init; }         // status line

            // Optional (nếu bạn có TargetPos + GO)
            public TextBox? TxbTargetPosDeg { get; init; }
            public Button? BtnGo { get; init; }
        }

        public sealed class MotionParameters
        {
            public int DefaultRpm { get; init; } = 300;
            public int MaxRpm { get; init; } = 2000;
            public float Acceleration { get; init; } = 100f;
            public float Deceleration { get; init; } = 100f;
            public int TrackingMinRpm { get; init; } = 30;
            public float PidKp { get; init; } = 20f;
            public float PidKi { get; init; }
            public float PidKd { get; init; }
        }

        // ===== Config per channel =====
        private readonly string _azCom;
        private readonly string _elCom;
        private readonly byte _azId;
        private readonly byte _elId;

        private readonly int _defaultRpm;
        private readonly int _modbusBaud;
        private readonly Func<MotionParameters> _motionParametersProvider;

        // ===== Host + manual buttons (shared area) =====
        private readonly Form _host;
        private readonly Control _btnAziLeft;
        private readonly Control _btnAziRight;
        private readonly Control _btnEleUp;
        private readonly Control _btnEleDown;

        // ===== Channels =====
        private readonly AxisChannel _az;
        private readonly AxisChannel _el;

        private bool _disposed;
        private bool _uiLocked;

        private sealed class AxisChannel
        {
            public readonly Axis Axis;
            public readonly AxisUi Ui;
            public readonly string Com;
            public readonly byte SlaveId;

            public ModbusRtuPort? Port;
            public KincoServoDrive? Drive;

            public bool Connected => Port != null && Drive != null;
            public bool Enabled;
            public bool Moving;

            public System.Windows.Forms.Timer? SpeedTimer;

            public CancellationTokenSource? GoCts;
            public CancellationTokenSource? MotionCts;
            public bool IsGoing;
            public int CommandedRpm;
            public DateTime? LastSpeedCommandUtc;
            public float IntegralErrorDegSec;
            public float LastErrorDeg;
            public DateTime? LastPidUtc;

            public AxisChannel(Axis axis, AxisUi ui, string com, byte slaveId)
            {
                Axis = axis;
                Ui = ui;
                Com = com;
                SlaveId = slaveId;
            }
        }

        // =========================
        // Constructor
        // =========================
        public Manual_Control(
            Form host,

            // 4 nút điều khiển tay
            Control btnAziLeft,
            Control btnAziRight,
            Control btnEleUp,
            Control btnEleDown,

            // UI từng kênh (độc lập)
            AxisUi azUi,
            AxisUi elUi,

            // config
            string azCom = "COM8",
            string elCom = "COM9",
            byte azId = 1,
            byte elId = 1,
            int defaultRpm = 300,
            int modbusBaud = 19200,
            Func<MotionParameters>? motionParametersProvider = null
        )
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            _btnAziLeft = btnAziLeft ?? throw new ArgumentNullException(nameof(btnAziLeft));
            _btnAziRight = btnAziRight ?? throw new ArgumentNullException(nameof(btnAziRight));
            _btnEleUp = btnEleUp ?? throw new ArgumentNullException(nameof(btnEleUp));
            _btnEleDown = btnEleDown ?? throw new ArgumentNullException(nameof(btnEleDown));

            _azCom = azCom;
            _elCom = elCom;
            _azId = azId;
            _elId = elId;
            _defaultRpm = defaultRpm;
            _modbusBaud = modbusBaud;
            _motionParametersProvider = motionParametersProvider ?? (() => new MotionParameters
            {
                DefaultRpm = _defaultRpm
            });

            _az = new AxisChannel(Axis.Azimuth, azUi ?? throw new ArgumentNullException(nameof(azUi)), _azCom, _azId);
            _el = new AxisChannel(Axis.Elevation, elUi ?? throw new ArgumentNullException(nameof(elUi)), _elCom, _elId);

            WireAxisUi(_az);
            WireAxisUi(_el);
            WireManualButtons();

            ApplyUiState(_az);
            ApplyUiState(_el);

            _host.FormClosing += Host_FormClosing;
        }

        // =========================
        // Public helpers (optional)
        // =========================
        public async Task RefreshPositionAsync(Axis axis)
        {
            var ch = GetCh(axis);
            if (!ch.Connected || ch.Drive == null) return;

            try
            {
                // Read actual position of motor
                int pos = await ch.Drive.GetActualPositionAsync().ConfigureAwait(true);

                
            }
            catch (Exception ex)
            {
                SetStatus(ch, $"Read pos error: {ex.Message}");
            }
        }

        public Task StartManualMoveAsync(Axis axis, int rpmSigned)
        {
            return StartMoveAsync(GetCh(axis), rpmSigned);
        }

        public Task StopManualMoveAsync(Axis axis)
        {
            return StopMoveAsync(GetCh(axis));
        }

        public async Task StopAllMotionAsync()
        {
            _az.GoCts?.Cancel();
            _el.GoCts?.Cancel();
            _az.MotionCts?.Cancel();
            _el.MotionCts?.Cancel();

            await Task.WhenAll(
                StopMoveAsync(_az),
                StopMoveAsync(_el)).ConfigureAwait(true);
        }

        public bool IsAxisConnected(Axis axis)
        {
            return GetCh(axis).Connected;
        }

        public bool IsAxisEnabled(Axis axis)
        {
            return GetCh(axis).Connected && GetCh(axis).Enabled;
        }

        public string GetAxisStatus(Axis axis)
        {
            var ch = GetCh(axis);
            if (!ch.Connected) return "Servo not connected";
            if (!ch.Enabled) return "Servo connected, not enabled";
            return ch.Moving ? "Servo moving" : "Servo ready";
        }

        public void RefreshUiState()
        {
            ApplyUiState(_az);
            ApplyUiState(_el);
        }

        public void SetUiLocked(bool locked)
        {
            _uiLocked = locked;
            ApplyUiState(_az);
            ApplyUiState(_el);
        }

        public Task TrackAxisToTargetAsync(Axis axis, float targetDeg, float toleranceDeg,
            CancellationToken ct = default)
        {
            return TrackAxisToTargetAsync(GetCh(axis), targetDeg, toleranceDeg, ct);
        }

        private void StartSpeedMonitor(AxisChannel ch)
        {
            StopSpeedMonitor(ch);

            ch.SpeedTimer = new System.Windows.Forms.Timer
            {
                Interval = 200 // ms (5Hz)
            };

            ch.SpeedTimer.Tick += async (_, __) =>
            {
                if (!ch.Connected || !ch.Enabled || ch.Drive == null) return;

                try
                {
                    int rpm = await ch.Drive.GetRealSpeedRpmAsync();

                    SafeUi(() =>
                    {
                        ch.Ui.TxbSpeedRpm.Text = rpm.ToString(CultureInfo.InvariantCulture);
                    });
                }
                catch
                {
                    // mất COM / timeout → không crash UI
                }
            };

            ch.SpeedTimer.Start();
        }

        private void StopSpeedMonitor(AxisChannel ch)
        {
            if (ch.SpeedTimer != null)
            {
                ch.SpeedTimer.Stop();
                ch.SpeedTimer.Dispose();
                ch.SpeedTimer = null;
            }
        }



        // =========================
        // Wiring per axis UI
        // =========================
        private void WireAxisUi(AxisChannel ch)
        {
            // Connect toggle
            ch.Ui.BtnConnect.Click += async (_, __) =>
            {
                if (!ch.Connected)
                    await ConnectAsync(ch).ConfigureAwait(true);
                else
                    await DisconnectAsync(ch).ConfigureAwait(true);
            };

            // Enable toggle
            ch.Ui.BtnEnable.Click += async (_, __) =>
            {
                if (!ch.Connected)
                {
                    SetStatus(ch, "Please Connect first.");
                    return;
                }

                if (!ch.Enabled)
                    await EnableAsync(ch).ConfigureAwait(true);
                else
                    await DisableAsync(ch).ConfigureAwait(true);
            };

            // Optional GO (target pos)
            if (ch.Ui.BtnGo != null && ch.Ui.TxbTargetPosDeg != null)
            {
                ch.Ui.BtnGo.Click += async (_, __) =>
                {
                    if (!ch.Connected || !ch.Enabled || ch.Drive == null)
                    {
                        SetStatus(ch, "Please Enable first.");
                        return;
                    }

                    if (!TryReadDeg(ch.Ui.TxbTargetPosDeg.Text, out var targetDeg))
                    {
                        SetStatus(ch, "Target Pos invalid.");
                        return;
                    }

                    // cancel previous GO if any
                    ch.GoCts?.Cancel();
                    ch.GoCts?.Dispose();
                    ch.GoCts = new CancellationTokenSource();

                    try
                    {
                        await GoToTargetAsync(ch, (float)targetDeg, ch.GoCts.Token).ConfigureAwait(true);
                    }
                    catch (OperationCanceledException)
                    {
                        ch.IsGoing = false;
                        ch.Moving = false;
                        ApplyUiState(ch);
                        SetStatus(ch, "GO canceled.");
                    }
                    catch (Exception ex)
                    {
                        SetStatus(ch, $"GO error: {ex.Message}");
                    }
                };
            }
        }

        private bool TryReadDeg(string s, out double deg)
        {
            s = s?.Trim() ?? "";
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out deg))
            {
                return true;
            }
            deg = 0;
            return false;
        }

        private static float WrapErrDeg(float err)
        {
            while (err > 180f) err -= 360f;
            while (err < -180f) err += 360f;
            return err;
        }

        private MotionParameters GetMotionParameters()
        {
            MotionParameters settings;
            try
            {
                settings = _motionParametersProvider();
            }
            catch
            {
                settings = new MotionParameters { DefaultRpm = _defaultRpm };
            }

            return new MotionParameters
            {
                DefaultRpm = Math.Max(1, settings.DefaultRpm),
                MaxRpm = Math.Clamp(settings.MaxRpm, 1, 4100),
                Acceleration = Math.Max(0f, settings.Acceleration),
                Deceleration = Math.Max(0f, settings.Deceleration),
                TrackingMinRpm = Math.Max(1, settings.TrackingMinRpm),
                PidKp = Math.Max(0f, settings.PidKp),
                PidKi = Math.Max(0f, settings.PidKi),
                PidKd = Math.Max(0f, settings.PidKd)
            };
        }

        private static int ClampTargetRpm(int rpmSigned, MotionParameters settings)
        {
            int maxRpm = Math.Clamp(settings.MaxRpm, 1, 4100);
            return Math.Clamp(rpmSigned, -maxRpm, maxRpm);
        }

        private static float GetRampRateRpmPerSec(int currentRpm, int targetRpm, MotionParameters settings)
        {
            bool acceleratingSameDirection =
                currentRpm == 0 ||
                Math.Sign(currentRpm) == Math.Sign(targetRpm) && Math.Abs(targetRpm) > Math.Abs(currentRpm);

            return acceleratingSameDirection ? settings.Acceleration : settings.Deceleration;
        }

        private static int ApplyRampLimit(AxisChannel ch, int targetRpm, MotionParameters settings)
        {
            targetRpm = ClampTargetRpm(targetRpm, settings);
            float rate = GetRampRateRpmPerSec(ch.CommandedRpm, targetRpm, settings);
            if (rate <= 0f) return targetRpm;

            DateTime now = DateTime.UtcNow;
            double elapsedSec = ch.LastSpeedCommandUtc.HasValue
                ? Math.Max(0.02, (now - ch.LastSpeedCommandUtc.Value).TotalSeconds)
                : 0.05;

            int maxDelta = Math.Max(1, (int)Math.Round(rate * elapsedSec));
            int delta = targetRpm - ch.CommandedRpm;
            if (Math.Abs(delta) <= maxDelta) return targetRpm;
            return ch.CommandedRpm + Math.Sign(delta) * maxDelta;
        }

        private async Task<int> SendSpeedRpmAsync(AxisChannel ch, int targetRpm, MotionParameters settings,
            bool rampToTarget, CancellationToken ct = default)
        {
            targetRpm = ClampTargetRpm(targetRpm, settings);

            do
            {
                int nextRpm = ApplyRampLimit(ch, targetRpm, settings);
                await ch.Drive!.SetTargetSpeedRpmAsync(nextRpm, ct).ConfigureAwait(true);
                ch.CommandedRpm = nextRpm;
                ch.LastSpeedCommandUtc = DateTime.UtcNow;

                if (!rampToTarget || nextRpm == targetRpm) return nextRpm;
                await Task.Delay(50, ct).ConfigureAwait(true);
            }
            while (!ct.IsCancellationRequested);

            return ch.CommandedRpm;
        }

        private void ResetPid(AxisChannel ch)
        {
            ch.IntegralErrorDegSec = 0f;
            ch.LastErrorDeg = 0f;
            ch.LastPidUtc = null;
        }

        private int CalculatePidSpeedRpm(AxisChannel ch, float errDeg, MotionParameters settings)
        {
            DateTime now = DateTime.UtcNow;
            float dtSec = ch.LastPidUtc.HasValue
                ? (float)Math.Max(0.02, (now - ch.LastPidUtc.Value).TotalSeconds)
                : 0.05f;

            ch.IntegralErrorDegSec = Math.Clamp(
                ch.IntegralErrorDegSec + errDeg * dtSec,
                -1000f,
                1000f);

            float derivativeDegPerSec = ch.LastPidUtc.HasValue
                ? (errDeg - ch.LastErrorDeg) / dtSec
                : 0f;

            ch.LastErrorDeg = errDeg;
            ch.LastPidUtc = now;

            float outputRpm =
                settings.PidKp * errDeg +
                settings.PidKi * ch.IntegralErrorDegSec +
                settings.PidKd * derivativeDegPerSec;

            int maxRpm = Math.Max(1, settings.MaxRpm);
            int minRpm = Math.Min(Math.Max(1, settings.TrackingMinRpm), maxRpm);
            int speedRpm = (int)Math.Round(Math.Clamp(outputRpm, -maxRpm, maxRpm));

            if (speedRpm == 0)
            {
                speedRpm = errDeg >= 0 ? minRpm : -minRpm;
            }
            else if (Math.Abs(speedRpm) < minRpm)
            {
                speedRpm = Math.Sign(speedRpm) * minRpm;
            }

            return ClampTargetRpm(speedRpm, settings);
        }

        private bool TryGetEncoderDeg (AxisChannel ch, out double currentDeg)
        {
            currentDeg = 0;
            var s = ch.Ui.TxbPositionDeg.Text?.Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out currentDeg))
            {
                return true;
            }
            return false;
        }

        
        private async Task GoToTargetAsync(AxisChannel ch, float targetDeg, CancellationToken ct)
        {
            if (ch.Drive == null) return;

            const float tolDeg = 0.2f; // sai số mục tiêu cho phép (theo độ)

            ch.IsGoing = true;
            ApplyUiState(ch);
            ResetPid(ch);
            SetStatus(ch, $"Go to {targetDeg:F2}° ...");

            // Đảm bảo động cơ ở Speed mode + Enabled
            await ch.Drive.SetOperationModeAsync(3, ct).ConfigureAwait(true);
            await ch.Drive.SetControlWordAsync(0x000F, ct).ConfigureAwait(true);

            while (!ct.IsCancellationRequested)
            {
                if (!TryGetEncoderDeg(ch, out var currentDeg))
                {
                    SetStatus(ch, "Read encoder error.");
                    break;
                }

                float errDeg = WrapErrDeg(targetDeg - (float)currentDeg);

                float absErrDeg = Math.Abs(errDeg);
                if (absErrDeg <= tolDeg)
                {
                    // Đạt mục tiêu
                    await SendSpeedRpmAsync(ch, 0, GetMotionParameters(), rampToTarget: true, ct).ConfigureAwait(true);
                    ResetPid(ch);
                    SetStatus(ch, "GO completed.");
                    break;
                }

                MotionParameters settings = GetMotionParameters();
                int speedRpm = CalculatePidSpeedRpm(ch, errDeg, settings);
                await SendSpeedRpmAsync(ch, speedRpm, settings, rampToTarget: false, ct).ConfigureAwait(true);

                //Cập nhật trạng thái
                SetStatus(ch, $"delta={errDeg:F2}°, V={speedRpm} RPM");

                // Chờ một thời gian trước khi lặp lại
                await Task.Delay(50, ct).ConfigureAwait(true);
            }

            ch.IsGoing = false;
            ch.Moving = false;
            ApplyUiState(ch);
        }

        private async Task TrackAxisToTargetAsync(AxisChannel ch, float targetDeg, float toleranceDeg,
            CancellationToken ct)
        {
            if (!ch.Connected || !ch.Enabled || ch.Drive == null)
            {
                SetStatus(ch, "Please Enable first.");
                return;
            }

            if (!TryGetEncoderDeg(ch, out var currentDeg))
            {
                SetStatus(ch, "Read encoder error.");
                return;
            }

            float errDeg = WrapErrDeg(targetDeg - (float)currentDeg);
            float absErrDeg = Math.Abs(errDeg);

            if (absErrDeg <= toleranceDeg)
            {
                int currentRpm = await SendSpeedRpmAsync(ch, 0, GetMotionParameters(), rampToTarget: false, ct).ConfigureAwait(true);
                ch.Moving = currentRpm != 0;
                ResetPid(ch);
                ApplyUiState(ch);
                SetStatus(ch, currentRpm == 0
                    ? $"Tracking OK {targetDeg:F2} deg"
                    : $"Tracking OK {targetDeg:F2} deg, slowing {currentRpm} RPM");
                return;
            }

            MotionParameters settings = GetMotionParameters();
            int speedRpm = CalculatePidSpeedRpm(ch, errDeg, settings);

            await ch.Drive.SetOperationModeAsync(3, ct).ConfigureAwait(true);
            await ch.Drive.SetControlWordAsync(0x000F, ct).ConfigureAwait(true);
            await SendSpeedRpmAsync(ch, speedRpm, settings, rampToTarget: false, ct).ConfigureAwait(true);

            ch.Moving = true;
            ApplyUiState(ch);
            SetStatus(ch, $"Tracking {targetDeg:F2} deg, delta={errDeg:F2}, V={speedRpm} RPM");
        }

        // =========================
        // Wiring manual move buttons (press = run, release/leave = stop)
        // =========================
        private void WireManualButtons()
        {
            // AZ left/right affects only AZ channel
            _btnAziLeft.MouseDown += (_, __) => _ = StartMoveAsync(_az, -ReadSpeedRpm(_az));
            _btnAziLeft.MouseUp += (_, __) => _ = StopMoveAsync(_az);
            _btnAziLeft.MouseLeave += (_, __) => _ = StopMoveAsync(_az);

            _btnAziRight.MouseDown += (_, __) => _ = StartMoveAsync(_az, +ReadSpeedRpm(_az));
            _btnAziRight.MouseUp += (_, __) => _ = StopMoveAsync(_az);
            _btnAziRight.MouseLeave += (_, __) => _ = StopMoveAsync(_az);

            // EL up/down affects only EL channel
            _btnEleUp.MouseDown += (_, __) => _ = StartMoveAsync(_el, +ReadSpeedRpm(_el));
            _btnEleUp.MouseUp += (_, __) => _ = StopMoveAsync(_el);
            _btnEleUp.MouseLeave += (_, __) => _ = StopMoveAsync(_el);

            _btnEleDown.MouseDown += (_, __) => _ = StartMoveAsync(_el, -ReadSpeedRpm(_el));
            _btnEleDown.MouseUp += (_, __) => _ = StopMoveAsync(_el);
            _btnEleDown.MouseLeave += (_, __) => _ = StopMoveAsync(_el);
        }

        private int ReadSpeedRpm(AxisChannel ch)
        {
            var s = ch.Ui.TxbSpeedRpm.Text?.Trim();
            MotionParameters settings = GetMotionParameters();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rpm))
            {
                rpm = Math.Abs(rpm);
                if (rpm == 0) rpm = settings.DefaultRpm;
                return Math.Min(rpm, settings.MaxRpm);
            }
            return Math.Min(settings.DefaultRpm, settings.MaxRpm);
        }

        // =========================
        // Connect / Disconnect
        // =========================
        private async Task ConnectAsync(AxisChannel ch)
        {
            try
            {
                SetStatus(ch, $"Opening {ch.Com} ...");

                // NOTE: nếu baud của Kinco khác thì chỉnh ở đây
                var port = new ModbusRtuPort(ch.Com, baud: _modbusBaud, parity: Parity.None, dataBits: 8, stopBits: StopBits.One);
                port.Open();

                var client = new ModbusClient(port);
                var drive = new KincoServoDrive(client, ch.SlaveId);

                // thử đọc vị trí để xác nhận kết nối OK
                int pos = await drive.GetActualPositionAsync().ConfigureAwait(true);

                ch.Port = port;
                ch.Drive = drive;
                ch.Enabled = false;
                ch.Moving = false;

                SetStatus(ch, "Connected.");
            }
            catch (Exception ex)
            {
                // cleanup
                try { ch.Port?.Dispose(); } catch { }
                ch.Port = null;
                ch.Drive = null;
                ch.Enabled = false;
                ch.Moving = false;

                //SetStatus(ch, $"Connect failed: {ex.Message}");
                SetStatus(ch, $"Connect failed!");
            }
            finally
            {
                ApplyUiState(ch);
            }
        }

        private async Task DisconnectAsync(AxisChannel ch)
        {
            try
            {
                // stop safely
                await StopMoveAsync(ch).ConfigureAwait(true);
                if (ch.Enabled) await DisableAsync(ch).ConfigureAwait(true);

                try { ch.Port?.Dispose(); } catch { }
                ch.Port = null;
                ch.Drive = null;
                ch.Enabled = false;
                ch.Moving = false;
               
                ch.GoCts?.Cancel();
                ch.GoCts?.Dispose();
                ch.GoCts = null;
                ch.IsGoing = false;

                SetStatus(ch, "Disconnected.");
            }
            finally
            {
                ApplyUiState(ch);
            }
        }

        // =========================
        // Enable / Disable (set mode + open brake)
        // =========================
        private async Task EnableAsync(AxisChannel ch)
        {
            if (!ch.Connected || ch.Drive == null) { ApplyUiState(ch); return; }

            try
            {
                SetStatus(ch, "Enabling...");

                await ch.Drive.SetOperationModeAsync(3).ConfigureAwait(true);      // speed mode
                await ch.Drive.SetControlWordAsync(0x000F).ConfigureAwait(true);  // enable (open brake)
                ch.Enabled = true;

                // Optional: start speed monitor timer
                StartSpeedMonitor(ch);

                SetStatus(ch, "Enabled.");
            }
            catch (Exception ex)
            {
                ch.Enabled = false;
                SetStatus(ch, $"Enable failed: {ex.Message}");
            }
            finally
            {
                ApplyUiState(ch);
            }
        }

        private async Task DisableAsync(AxisChannel ch)
        {
            if (!ch.Connected || ch.Drive == null) { ApplyUiState(ch); return; }

            try
            {
                SetStatus(ch, "Disabling...");
                ch.MotionCts?.Cancel();
                ch.MotionCts?.Dispose();
                ch.MotionCts = new CancellationTokenSource();
                await SendSpeedRpmAsync(ch, 0, GetMotionParameters(), rampToTarget: true, ch.MotionCts.Token).ConfigureAwait(true);
                await ch.Drive.SetControlWordAsync(6).ConfigureAwait(true); // disable/quick stop
                ch.Enabled = false;
                ch.Moving = false;
                ResetPid(ch);

                // Optional: stop speed monitor timer
                StopSpeedMonitor(ch);

                ch.GoCts?.Cancel();
                ch.GoCts?.Dispose();
                ch.GoCts = null;
                ch.IsGoing = false;

                SetStatus(ch, "Disabled.");
            }
            catch (Exception ex)
            {
                SetStatus(ch, $"Disable failed: {ex.Message}");
            }
            finally
            {
                ApplyUiState(ch);
            }
        }

        // =========================
        // Move / Stop (only if Enabled)
        // =========================
        private async Task StartMoveAsync(AxisChannel ch, int rpmSigned)
        {
            if (!ch.Connected || ch.Drive == null)
            {
                SetStatus(ch, "Please Connect first.");
                return;
            }
            if (!ch.Enabled)
            {
                SetStatus(ch, "Please Enable first.");
                return;
            }
            if (ch.Moving) return;

            try
            {
                ch.Moving = true;
                ApplyUiState(ch);
                ch.MotionCts?.Cancel();
                ch.MotionCts?.Dispose();
                ch.MotionCts = new CancellationTokenSource();
                MotionParameters settings = GetMotionParameters();

                await ch.Drive.SetOperationModeAsync(3).ConfigureAwait(true);
                await ch.Drive.SetControlWordAsync(0x000F).ConfigureAwait(true);
                await SendSpeedRpmAsync(ch, rpmSigned, settings, rampToTarget: true, ch.MotionCts.Token).ConfigureAwait(true);

                SetStatus(ch, $"Moving {ClampTargetRpm(rpmSigned, settings)} RPM");
            }
            catch (OperationCanceledException)
            {
                // A stop command or a new move command replaced this ramp.
            }
            catch (Exception ex)
            {
                ch.Moving = false;
                ApplyUiState(ch);
                SetStatus(ch, $"Move error: {ex.Message}");
            }
        }

        private async Task StopMoveAsync(AxisChannel ch)
        {
            if (!ch.Connected || ch.Drive == null) return;

            try
            {
                ch.MotionCts?.Cancel();
                ch.MotionCts?.Dispose();
                ch.MotionCts = new CancellationTokenSource();
                await SendSpeedRpmAsync(ch, 0, GetMotionParameters(), rampToTarget: true, ch.MotionCts.Token).ConfigureAwait(true);
                // giữ CW theo trạng thái enable:
                if (ch.Enabled)
                    await ch.Drive.SetControlWordAsync(0x000F).ConfigureAwait(true);
                else
                    await ch.Drive.SetControlWordAsync(6).ConfigureAwait(true);

                ch.Moving = false;
                ch.IsGoing = false;
                ResetPid(ch);
                ApplyUiState(ch);

                // nếu enable thì hiển thị stopped, nếu chưa enable thì giữ status hiện tại
                if (ch.Enabled) SetStatus(ch, "Stopped.");
            }
            catch (OperationCanceledException)
            {
                // A new command replaced this stop ramp.
            }
            catch (Exception ex)
            {
                SetStatus(ch, $"Stop error: {ex.Message}");
            }
        }

        // =========================
        // UI State
        // =========================
        private void ApplyUiState(AxisChannel ch)
        {
            SafeUi(() =>
            {
                if (_uiLocked)
                {
                    ch.Ui.BtnConnect.Enabled = false;
                    ch.Ui.BtnEnable.Enabled = false;
                    if (ch.Ui.BtnGo != null) ch.Ui.BtnGo.Enabled = false;
                    if (ch.Ui.TxbTargetPosDeg != null) ch.Ui.TxbTargetPosDeg.Enabled = false;
                    return;
                }

                // Connect button text
                ch.Ui.BtnConnect.Text = ch.Connected ? "Disconnect" : "Connect";

                // Enable button enabled only when connected
                ch.Ui.BtnEnable.Enabled = ch.Connected;
                ch.Ui.BtnEnable.Text = ch.Enabled ? "Disable" : "Enable";

                // Optional GO
                bool axisReadyForGo = ch.Connected && ch.Enabled && !ch.Moving && !ch.IsGoing;
                if (ch.Ui.BtnGo != null) ch.Ui.BtnGo.Enabled = axisReadyForGo;
                if (ch.Ui.TxbTargetPosDeg != null) ch.Ui.TxbTargetPosDeg.Enabled = ch.Connected && ch.Enabled;
            });
        }

        private void SetStatus(AxisChannel ch, string text)
        {
            SafeUi(() => ch.Ui.LblStatus.Text = text);
        }

        private AxisChannel GetCh(Axis axis) => axis == Axis.Azimuth ? _az : _el;

        private void SafeUi(Action act)
        {
            if (_host.IsDisposed) return;
            if (_host.InvokeRequired) _host.BeginInvoke(act);
            else act();
        }

        // =========================
        // Dispose / Closing
        // =========================
        private bool _closing;
        private async void Host_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_closing) return;

            // chặn không cho form đóng ngay
            e.Cancel = true;
            _closing = true;

            try
            {
                // TẮT an toàn trước khi thoát (đóng phanh)
                await SafeShutdownAsync().ConfigureAwait(true);
            }
            catch { }
            finally
            {
                // Sau khi shutdown xong -> cho đóng form thật sự
                _host.BeginInvoke(new Action(() => _host.Close()));
            }
        }


        private async Task SafeShutdownAsync()
        {
            // Dừng GO nếu có
            _az.GoCts?.Cancel();
            _el.GoCts?.Cancel();

            // Stop move trước
            await StopMoveAsync(_az).ConfigureAwait(true);
            await StopMoveAsync(_el).ConfigureAwait(true);

            // QUAN TRỌNG: Disable (ControlWord=6) để đóng phanh nếu đang enable
            if (_az.Enabled) await DisableAsync(_az).ConfigureAwait(true);
            if (_el.Enabled) await DisableAsync(_el).ConfigureAwait(true);

            // Sau đó mới Disconnect (dispose port)
            await DisconnectAsync(_az).ConfigureAwait(true);
            await DisconnectAsync(_el).ConfigureAwait(true);

            Dispose();
        }


        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _host.FormClosing -= Host_FormClosing; } catch { }

            try { _az.Port?.Dispose(); } catch { }
            try { _el.Port?.Dispose(); } catch { }

            _az.Port = null; _az.Drive = null;
            _el.Port = null; _el.Drive = null;
            _az.Drive = null;
            _el.Drive = null;
        }
    }
}
