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

        // last time LSB changed (per encoder)
        private DateTime _lastE1LsbChangeUtc = DateTime.MinValue;
        private DateTime _lastE2LsbChangeUtc = DateTime.MinValue;

        private int _lastE1Lsb = -1;
        private int _lastE2Lsb = -1;

        private bool _e1Stuck;
        private bool _e2Stuck;

        private const int NoDataTimeoutMs = 1500;     // mạch treo/không gửi gì
        private const int LsbStuckTimeoutMs = 1200;   // mất encoder (LSB single-turn không đổi)

        public bool IsConnected => _sp != null && _sp.IsOpen;

        // ===== Regex parse STM32 =====
        private static readonly Regex LineRx = new Regex(
            @"E1:(?<e1st>\d+),(?<e1ang>[-+]?\d+(?:\.\d+)?),(?<e1err>\d+),(?<e1warn>\d+)\s*\|\s*" +  // E1:single_turn,angle,error,warning
            @"E2:(?<e2st>\d+),(?<e2ang>[-+]?\d+(?:\.\d+)?),(?<e2err>\d+),(?<e2warn>\d+)",           // E2:single_turn,angle,error,warning
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // ===== Public latest values =====
        public float AziDeg { get; private set; }
        public float EleDeg { get; private set; }
        public bool AziErr { get; private set; }
        public bool EleErr { get; private set; }

        public EncoderComReader(Form host, TextBox AziPos, TextBox ElePos, string com, int baud)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _com = com;
            _baud = baud;
            _txbAziPos = AziPos;
            _txbElePos = ElePos;

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

            AziDeg = float.Parse(m.Groups["e1ang"].Value, CultureInfo.InvariantCulture);
            EleDeg = float.Parse(m.Groups["e2ang"].Value, CultureInfo.InvariantCulture);

            AziErr = m.Groups["e1err"].Value != "0";
            EleErr = m.Groups["e2err"].Value != "0";

            AziErr = m.Groups["e1err"].Value != "0";
            EleErr = m.Groups["e2err"].Value != "0";

            // --- NEW: monitor single-turn LSB for each encoder ---
            if (int.TryParse(m.Groups["e1st"].Value, out int e1st))
            {
                int lsb = e1st & 1;
                if (_lastE1Lsb == -1)
                {
                    _lastE1Lsb = lsb;
                    _lastE1LsbChangeUtc = DateTime.UtcNow; // khởi tạo
                }
                else if (lsb != _lastE1Lsb)
                {
                    _lastE1Lsb = lsb;
                    _lastE1LsbChangeUtc = DateTime.UtcNow;
                    _e1Stuck = false;
                }
            }

            if (int.TryParse(m.Groups["e2st"].Value, out int e2st))
            {
                int lsb = e2st & 1;
                if (_lastE2Lsb == -1)
                {
                    _lastE2Lsb = lsb;
                    _lastE2LsbChangeUtc = DateTime.UtcNow;
                }
                else if (lsb != _lastE2Lsb)
                {
                    _lastE2Lsb = lsb;
                    _lastE2LsbChangeUtc = DateTime.UtcNow;
                    _e2Stuck = false;
                }
            }

            // mark we are alive (received a full valid line)
            _lastGoodLineUtc = DateTime.UtcNow;

            // Update UI respecting stuck state (ERR/NORMAL)
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
                    _e1Stuck = _e2Stuck = false;
                    return;
                }
            }

            // Case (2): still receiving lines, but 1 encoder stuck (LSB not changing)
            // Only evaluate stuck if we have seen at least 1 LSB change time
            if (_lastGoodLineUtc != DateTime.MinValue)
            {
                if (_lastE1LsbChangeUtc != DateTime.MinValue)
                    _e1Stuck = (now - _lastE1LsbChangeUtc).TotalMilliseconds > LsbStuckTimeoutMs;

                if (_lastE2LsbChangeUtc != DateTime.MinValue)
                    _e2Stuck = (now - _lastE2LsbChangeUtc).TotalMilliseconds > LsbStuckTimeoutMs;

                UpdateUiNow(); // refresh textbox according to stuck flags
            }
        }

        private void UpdateUiNow()
        {
            // Nếu encoder kênh nào lỗi -> báo ERR riêng kênh đó
            string aziText = _e1Stuck ? "ERR" : AziDeg.ToString("0.###", CultureInfo.InvariantCulture);
            string eleText = _e2Stuck ? "ERR" : EleDeg.ToString("0.###", CultureInfo.InvariantCulture);
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
