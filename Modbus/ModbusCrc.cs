using System;

namespace Modbus;

public static class ModbusCrc
{
    public static ushort ComputeCRC(byte[] data, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = 0; i < length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                bool lsb = (crc & 0x0001) != 0;
                crc >>= 1;
                if (lsb) crc ^= 0xA001;
            }
        }
        return crc;
    }

    public static byte[] AppendCRC(byte[] data)
    {
        ushort crc = ComputeCRC(data, data.Length);
        byte[] result = new byte[data.Length + 2];
        Array.Copy(data, result, data.Length);
        result[^2] = (byte)(crc & 0xFF);
        result[^1] = (byte)((crc >> 8) & 0xFF);
        return result;
    }

    public static bool ValidateFrame(byte[] frame)
    {
        if (frame.Length < 5) return false;
        int lenNoCrc = frame.Length - 2;
        ushort recv = (ushort)(frame[^2] | (frame[^1] << 8));
        ushort calc = ComputeCRC(frame, lenNoCrc);
        return recv == calc;
    }
}
