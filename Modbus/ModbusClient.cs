using System.Threading;
using System.Threading.Tasks;

namespace Modbus;

public sealed class ModbusClient
{
    private readonly ModbusRtuPort _port;
    public ModbusClient(ModbusRtuPort port) => _port = port;

    public Task<byte[]> ReadHoldingAsync(byte id, ushort addr, ushort count, int timeoutMs = 1000, CancellationToken ct = default)
    {
        byte[] req = { id, 0x03, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(count >> 8), (byte)(count & 0xFF) };
        return _port.TxRxAsync(req, timeoutMs, ct);
    }

    public Task<byte[]> WriteSingleAsync(byte id, ushort addr, ushort value, int timeoutMs = 1000, CancellationToken ct = default)
    {
        byte[] req = { id, 0x06, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(value >> 8), (byte)(value & 0xFF) };
        return _port.TxRxAsync(req, timeoutMs, ct);
    }

    public Task<byte[]> WriteMultiAsync(byte id, ushort addr, ushort[] values, int timeoutMs = 1000, CancellationToken ct = default)
    {
        int byteCount = values.Length * 2;
        byte[] req = new byte[7 + byteCount];
        req[0] = id; req[1] = 0x10;
        req[2] = (byte)(addr >> 8); req[3] = (byte)(addr & 0xFF);
        req[4] = (byte)(values.Length >> 8); req[5] = (byte)(values.Length & 0xFF);
        req[6] = (byte)byteCount;

        int k = 7;
        foreach (var v in values)
        {
            req[k++] = (byte)(v >> 8);
            req[k++] = (byte)(v & 0xFF);
        }

        return _port.TxRxAsync(req, timeoutMs, ct);
    }
}
