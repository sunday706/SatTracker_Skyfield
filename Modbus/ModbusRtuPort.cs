using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Modbus;

public sealed class ModbusRtuPort : IDisposable
{
    private readonly SerialPort _port;
    private readonly System.Timers.Timer _gapTimer;

    private readonly object _rxLock = new();
    private byte[] _rxBuf = new byte[512];
    private int _rxLen = 0;

    private readonly object _pendingLock = new();
    private TaskCompletionSource<byte[]>? _pending;

    // đảm bảo 1 COM chỉ có 1 request đang chờ response
    private readonly SemaphoreSlim _txrxLock = new(1, 1);

    public string PortName => _port.PortName;
    public bool IsOpen => _port.IsOpen;

    public ModbusRtuPort(string portName, int baud = 19200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _port = new SerialPort(portName, baud, parity, dataBits, stopBits)
        {
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        _port.DataReceived += OnDataReceived;

        _gapTimer = new System.Timers.Timer(20);
        _gapTimer.AutoReset = false;
        _gapTimer.Elapsed += (_, __) => OnFrameGap();
    }

    public void Open() => _port.Open();
    public void Close() => _port.Close();

    public async Task<byte[]> TxRxAsync(byte[] reqNoCrc, int timeoutMs = 1000, CancellationToken ct = default)
    {
        if (!_port.IsOpen) throw new InvalidOperationException("Serial port not open.");

        await _txrxLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var req = ModbusCrc.AppendCRC(reqNoCrc);

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pendingLock)
            {
                if (_pending != null) throw new InvalidOperationException("A request is already pending on this port.");
                _pending = tcs;
            }

            _port.Write(req, 0, req.Length);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await using (cts.Token.Register(() =>
            {
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(ct);
                }
                else
                {
                    tcs.TrySetException(new TimeoutException(
                        $"No Modbus response from {_port.PortName} within {timeoutMs} ms."));
                }
            }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_pendingLock) _pending = null;
            _txrxLock.Release();
        }
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        int count = _port.BytesToRead;
        if (count <= 0) return;

        byte[] tmp = new byte[count];
        int n = _port.Read(tmp, 0, count);
        if (n <= 0) return;

        lock (_rxLock)
        {
            int canCopy = Math.Min(n, _rxBuf.Length - _rxLen);
            if (canCopy <= 0) { _rxLen = 0; canCopy = Math.Min(n, _rxBuf.Length); }

            Array.Copy(tmp, 0, _rxBuf, _rxLen, canCopy);
            _rxLen += canCopy;

            _gapTimer.Stop();
            _gapTimer.Start();
        }
    }

    private void OnFrameGap()
    {
        byte[] frame;
        lock (_rxLock)
        {
            if (_rxLen == 0) return;
            frame = new byte[_rxLen];
            Array.Copy(_rxBuf, frame, _rxLen);
            _rxLen = 0;
        }

        if (!ModbusCrc.ValidateFrame(frame)) return;

        TaskCompletionSource<byte[]>? tcs;
        lock (_pendingLock) tcs = _pending;
        tcs?.TrySetResult(frame);
    }

    public void Dispose()
    {
        try { _gapTimer?.Stop(); _gapTimer?.Dispose(); } catch { }
        try
        {
            _port.DataReceived -= OnDataReceived;
            if (_port.IsOpen) _port.Close();
            _port.Dispose();
        }
        catch { }
    }
}
