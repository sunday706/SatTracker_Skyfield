using System.Threading;
using System.Threading.Tasks;
using Modbus;

namespace Kinco;

public sealed class KincoServoDrive
{
    private readonly ModbusClient _mb;
    public byte SlaveId { get; }

    public KincoServoDrive(ModbusClient mb, byte slaveId)
    {
        _mb = mb;
        SlaveId = slaveId;
    }

    public Task SetOperationModeAsync(ushort mode, CancellationToken ct = default)
        => _mb.WriteSingleAsync(SlaveId, 0x3500, mode, ct: ct);

    public Task SetControlWordAsync(ushort word, CancellationToken ct = default)
        => _mb.WriteSingleAsync(SlaveId, 0x3100, word, ct: ct);

    public Task SetTargetSpeedRpmAsync(int rpm, CancellationToken ct = default)
    {
        const int SERVO_ENCODER_RESOLUTION = 2500;
        const int EncoderCounts = SERVO_ENCODER_RESOLUTION * 4;
        const int SERVO_MAX_RPM = 4100;

        if (rpm > SERVO_MAX_RPM) rpm = SERVO_MAX_RPM;
        if (rpm < -SERVO_MAX_RPM) rpm = -SERVO_MAX_RPM;

        long dec = (long)rpm * 512L * EncoderCounts / 1875L;
        int dec32 = unchecked((int)dec);

        ushort low = (ushort)(dec32 & 0xFFFF);
        ushort high = (ushort)((dec32 >> 16) & 0xFFFF);

        return _mb.WriteMultiAsync(SlaveId, 0x6F00, new ushort[] { low, high }, ct: ct);
    }

    public async Task<int> GetActualPositionAsync(CancellationToken ct = default)
    {
        var resp = await _mb.ReadHoldingAsync(SlaveId, 0x3700, 2, ct: ct);
        // resp: [id][03][byteCount][data...][crcLo][crcHi]
        ushort reg0 = (ushort)((resp[3] << 8) | resp[4]);
        ushort reg1 = (ushort)((resp[5] << 8) | resp[6]);
        return (int)((reg1 << 16) | reg0);
    }

    public async Task<int> GetRealSpeedRpmAsync(CancellationToken ct = default)
    {
        var resp = await _mb.ReadHoldingAsync(SlaveId, 0x3B00, 2, ct: ct);
        // resp: [id][03][byteCount][data...][crcLo][crcHi]
        ushort reg0 = (ushort)((resp[3] << 8) | resp[4]);
        ushort reg1 = (ushort)((resp[5] << 8) | resp[6]);
        int dec32 = (int)((reg1 << 16) | reg0);
        const int SERVO_ENCODER_RESOLUTION = 2500;
        const int EncoderCounts = SERVO_ENCODER_RESOLUTION * 4;
        long rpm = (long)dec32 * 1875L / (512L * EncoderCounts);
        return (int)rpm;
    }
}
