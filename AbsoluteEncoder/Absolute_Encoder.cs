using System;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SatTracker.AntenControl
{
    public sealed class EncoderComReader : IDisposable
    {
        // ===== UI =====
        private readonly Form _host;
        private Label _lblEnc;
        private TextBox _txbAziPos;
        private TextBox _txbElePos;

        // ===== Serial / config =====
        private SerialPort? _sp;
        private readonly string _com;
        private readonly int _baud;

        private readonly StringBuilder _buf = new();

        // Retry timer (WinForms timer -> chạy trên UI thread)
        private readonly System.Windows.Forms.Timer _reconnectTimer;

        private readonly System.Windows.Forms.Timer _watchdogTimer;

        // last time we successfully parsed a full line (E1|E2)
        private DateTime _lastGoodLineUtc = DateTime.MinValue;

        private const int NoDataTimeoutMs = 1500;     // mạch treo/không gửi gì

        public bool IsConnected => _sp != null && _sp.IsOpen;

        // ===== Regex parse STM32 =====
        private static readonly Regex LineRx = new Regex(
            @"E1:(?<e1st>\d+),(?<e1ang>[-+]?\d+(?:\.\d+)?),(?<e1err>\d+),(?<e1warn>\d+)\s*\|\s*" +  // E1:single_turn,angle,error,warning
            @"E2:(?<e2st>\d+),(?<e2ang>[-+]?\d+(?:\.\d+)?),(?<e2err>\d+),(?<e2warn>\d+)",           // E2:single_turn,angle,error,warning
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // ===== Public latest values =====
        public float AziDeg { get; private set; }
        public float EleDeg { get; private set; }
        public float RawAziDeg { get; private set; }
        public float RawEleDeg { get; private set; }
        public bool AziErr { get; private set; }
        public bool EleErr { get; private set; }
        public bool HasValidData => IsConnected &&
            _lastGoodLineUtc != DateTime.MinValue &&
            (DateTime.UtcNow - _lastGoodLineUtc).TotalMilliseconds <= NoDataTimeoutMs;
        public bool IsAziReady => HasValidData;
        public bool IsEleReady => HasValidData;

        private float _aziHomeRawDeg;
        private float _eleHomeRawDeg;
        private float _aziHomeDeg;
        private float _eleHomeDeg;

        public EncoderComReader(Form host, TextBox AziPos, TextBox ElePos, string com, int baud,
            float aziHomeRawDeg = 0, float eleHomeRawDeg = 0, float aziHomeDeg = 0, float eleHomeDeg = 0)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _com = com;
            _baud = baud;
            _txbAziPos = AziPos;
            _txbElePos = ElePos;
            ApplyHomeCalibration(aziHomeRawDeg, eleHomeRawDeg, aziHomeDeg, eleHomeDeg);

            _reconnectTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000 // 2s retry
            };
            _reconnectTimer.Tick += (_, __) => TryOpen();
            _watchdogTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _watchdogTimer.Tick += (_, __) => WatchdogTick();
            _watchdogTimer.Start();

            // thử mở lần đầu (không được thì không crash)
            TryOpen();

            // auto dispose khi form đóng
            _host.FormClosing += Host_FormClosing;
        }

        public void ApplyHomeCalibration(float aziHomeRawDeg, float eleHomeRawDeg, float aziHomeDeg, float eleHomeDeg)
        {
            _aziHomeRawDeg = aziHomeRawDeg;
            _eleHomeRawDeg = eleHomeRawDeg;
            _aziHomeDeg = aziHomeDeg;
            _eleHomeDeg = eleHomeDeg;

            AziDeg = NormalizeAzimuthDeg(RawAziDeg - _aziHomeRawDeg + _aziHomeDeg);
            EleDeg = RawEleDeg - _eleHomeRawDeg + _eleHomeDeg;
            UpdateUiNow();
        }

        private static float NormalizeAzimuthDeg(float deg)
        {
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return deg;
        }

        private void Host_FormClosing(object? sender, FormClosingEventArgs e) => Dispose();

        // ===============================
        // UI
        // ===============================
        private void UpdateStatus(string encAzi, string encEle)
        {
            if (_host.IsDisposed) return;

            if (_host.InvokeRequired)
            {
                _host.BeginInvoke(new Action(() =>
                {

                    _txbAziPos.Text = encAzi;
                    _txbElePos.Text = encEle;
                }));
            }
            else
            {
                _txbAziPos.Text = encAzi;
                _txbElePos.Text = encEle;
            }
        }


        // ===============================
        // Connect / reconnect
        // ===============================
        private void TryOpen()
        {
            // Nếu đã mở rồi thì thôi
            if (IsConnected)
            {
                _reconnectTimer.Stop();
                return;
            }

            // Dọn port cũ (nếu có)
            SafeClosePort();

            try
            {
                var sp = new SerialPort(_com, _baud, Parity.None, 8, StopBits.One)
                {
                    Encoding = Encoding.ASCII,
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    NewLine = "\r\n"
                };

                sp.DataReceived += OnDataReceived;
                sp.Open();

                _sp = sp;

                _reconnectTimer.Stop();
            }
            catch (Exception ex)
            {
                UpdateStatus("N/A", "N/A");
                if (!_reconnectTimer.Enabled)
                    _reconnectTimer.Start();
            }
        }

        private void SafeClosePort()
        {
            try
            {
                if (_sp != null)
                {
                    try { _sp.DataReceived -= OnDataReceived; } catch { }

                    if (_sp.IsOpen)
                        _sp.Close();

                    _sp.Dispose();
                }
            }
            catch { }
            finally
            {
                _sp = null;
                _buf.Clear();
            }
        }

        // ===============================
        // Serial receive
        // ===============================
        private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
        {
            // Nếu đang mất kết nối thì bỏ qua
            if (_sp == null || !_sp.IsOpen) return;

            try
            {
                string chunk = _sp.ReadExisting();
                if (string.IsNullOrEmpty(chunk))  // không có gì mới
                {
                    UpdateStatus("N/A", "N/A");
                    return;
                }
                    

                _buf.Append(chunk); // thêm vào buffer

                while (true)
                {
                    var s = _buf.ToString();
                    int idx = s.IndexOf('\n');
                    if (idx < 0) break;

                    string line = s.Substring(0, idx).Trim();  // lấy dòng
                    _buf.Remove(0, idx + 1); // xóa dòng đã lấy khỏi buffer

                    ParseLine(line); // phân tích dòng
                }
            }
            catch (Exception ex)
            {
                // Mất COM / lỗi IO trong lúc đang chạy -> chuyển sang retry
                UpdateStatus("N/A", "N/A");
                TryOpen();
            }
        }

        // ===============================
        // Parse STM32 line
        // ===============================
        private void ParseLine(string line)
        {
            var m = LineRx.Match(line);
            if (!m.Success) return;

            RawAziDeg = float.Parse(m.Groups["e1ang"].Value, CultureInfo.InvariantCulture);
            RawEleDeg = float.Parse(m.Groups["e2ang"].Value, CultureInfo.InvariantCulture);
            AziDeg = NormalizeAzimuthDeg(RawAziDeg - _aziHomeRawDeg + _aziHomeDeg);
            EleDeg = RawEleDeg - _eleHomeRawDeg + _eleHomeDeg;

            AziErr = m.Groups["e1err"].Value != "0";
            EleErr = m.Groups["e2err"].Value != "0";

            // mark we are alive (received a full valid line)
            _lastGoodLineUtc = DateTime.UtcNow;

            UpdateUiNow();


        }

        private void WatchdogTick()
        {
            if (_host.IsDisposed) return;

            var now = DateTime.UtcNow;

            // Case (1): COM open but no valid line for too long
            if (IsConnected && _lastGoodLineUtc != DateTime.MinValue)
            {
                var silentMs = (now - _lastGoodLineUtc).TotalMilliseconds;
                if (silentMs > NoDataTimeoutMs)
                {
                    // báo không đọc được gì
                    UpdateStatus("N/A", "N/A");
                    return;
                }
            }

            if (_lastGoodLineUtc != DateTime.MinValue)
            {
                UpdateUiNow();
            }
        }

        private void UpdateUiNow()
        {
            string aziText = AziDeg.ToString("0.###", CultureInfo.InvariantCulture);
            string eleText = EleDeg.ToString("0.###", CultureInfo.InvariantCulture);
            UpdateStatus(aziText, eleText);
        }

        // ===============================
        // Dispose
        // ===============================
        public void Dispose()
        {
            try { _host.FormClosing -= Host_FormClosing; } catch { }

            try
            {
                _reconnectTimer.Stop();
                _reconnectTimer.Dispose();
            }
            catch { }

            SafeClosePort();
        }
    }
}
