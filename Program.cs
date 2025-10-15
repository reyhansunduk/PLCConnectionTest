using System;
using Sharp7;

namespace PlcUtilities
{
    public class PlcDataSnapshot
    {
        public bool FlagA { get; set; }
        public bool FlagB { get; set; }
        public bool FlagC { get; set; }
        public string TextData1 { get; set; } = string.Empty;
        public string TextData2 { get; set; } = string.Empty;
        public byte[] RawBytes { get; set; } = Array.Empty<byte>();
    }

    public class PlcClientHelper : IDisposable
    {
        private readonly S7Client _client = new S7Client();
        private readonly string _ip;
        private readonly int _rack;
        private readonly int _slot;
        private bool _disposed = false;

        // Sharp7 sürümünüzde Connected property'si bool olabilir; buna göre döndürüyoruz.
        public bool IsConnected => _client.Connected;

        public PlcClientHelper(string ip, int rack = 0, int slot = 1)
        {
            _ip = ip;
            _rack = rack;
            _slot = slot;
        }

        // Genel hata metni alma arayüzü (dışarıdan _client.ErrorText'e erişmeden kullanın)
        public string GetErrorText(int errorCode) => _client.ErrorText(errorCode);

        public int Connect(int maxRetries = 1, int retryDelayMs = 500)
        {
            int res;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                res = _client.ConnectTo(_ip, _rack, _slot);
                if (res == 0) return 0;
                System.Threading.Thread.Sleep(retryDelayMs);
            }

            res = _client.ConnectTo(_ip, _rack, _slot);
            return res;
        }

        public void Disconnect()
        {
            try
            {
                // Sharp7Client'de Connected bool döndüğünden doğrudan kontrol edelim
                if (_client.Connected)
                {
                    _client.Disconnect();
                }
            }
            catch { }
        }

        public (int result, byte[] buffer) ReadDbRaw(int dbNumber, int startByte, int length)
        {
            if (length <= 0) throw new ArgumentException("length must be > 0", nameof(length));
            var buf = new byte[length];
            int res = _client.DBRead(dbNumber, startByte, length, buf);
            return (res, buf);
        }

        public int WriteDbRaw(int dbNumber, int startByte, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return _client.DBWrite(dbNumber, startByte, data.Length, data);
        }

        public PlcDataSnapshot ReadDb1Snapshot()
        {
            const int DB_NUMBER = 1;
            const int LENGTH = 32;

            var (res, buf) = ReadDbRaw(DB_NUMBER, 0, LENGTH);
            if (res != 0)
                throw new InvalidOperationException($"DBRead failed ({res}): {GetErrorText(res)}");

            var data = new PlcDataSnapshot { RawBytes = buf };

            data.FlagA = SafeGetBit(buf, 0, 0);
            data.FlagB = SafeGetBit(buf, 1, 0);
            data.FlagC = SafeGetBit(buf, 2, 1);

            data.TextData1 = SafeGetString(buf, 4, 8);
            data.TextData2 = SafeGetString(buf, 12, 8);

            return data;
        }

        private static bool SafeGetBit(byte[] buffer, int byteIndex, int bitIndex)
        {
            if (buffer == null || byteIndex < 0 || byteIndex >= buffer.Length || bitIndex < 0 || bitIndex > 7)
                return false;
            return S7.GetBitAt(buffer, byteIndex, bitIndex);
        }

        private static string SafeGetString(byte[] buffer, int start, int length)
        {
            if (buffer == null || start < 0 || start >= buffer.Length || length <= 0)
                return string.Empty;

            int available = Math.Min(length, buffer.Length - start);
            try
            {
                string raw = S7.GetCharsAt(buffer, start, available);
                return raw.Trim('\0', ' ', '\r', '\n');
            }
            catch { return string.Empty; }
        }

        public static void PrettyPrintBuffer(byte[] buffer)
        {
            if (buffer == null) return;
            Console.WriteLine("\n--- Raw DB bytes (HEX) ---");
            Console.WriteLine(BitConverter.ToString(buffer));

            Console.WriteLine("\n--- Raw DB bytes (DEC) ---");
            for (int i = 0; i < buffer.Length; i++)
            {
                Console.Write($"[{i}]={buffer[i]:D3} ");
                if ((i + 1) % 8 == 0) Console.WriteLine();
            }
            Console.WriteLine();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            _disposed = true;
        }
    }

    public static class Program
    {
        public static void Main()
        {
            Console.WriteLine("=== Generic S7-1500 PLC Connection Test (Fixed) ===\n");

            string plcIp = "192.168.0.10";
            int rack = 0;
            int slot = 1;

            using var plc = new PlcClientHelper(plcIp, rack, slot);

            Console.WriteLine($"Connecting to PLC {plcIp} (rack={rack}, slot={slot})...");
            int connRes = plc.Connect(maxRetries: 2, retryDelayMs: 300);

            if (connRes != 0)
            {
                Console.WriteLine($"❌ CONNECTION FAILED: {connRes} - {plc.GetErrorText(connRes)}");
                return;
            }

            Console.WriteLine("✅ PLC CONNECTED!\n");

            try
            {
                var snapshot = plc.ReadDb1Snapshot();

                Console.WriteLine("--- PLC Data Snapshot ---");
                Console.WriteLine($"FlagA: {snapshot.FlagA}");
                Console.WriteLine($"FlagB: {snapshot.FlagB}");
                Console.WriteLine($"FlagC: {snapshot.FlagC}");
                Console.WriteLine($"TextData1: '{snapshot.TextData1}'");
                Console.WriteLine($"TextData2: '{snapshot.TextData2}'");

                PlcClientHelper.PrettyPrintBuffer(snapshot.RawBytes);

                Console.WriteLine("\n--- DB2 Write Example ---");
                byte[] writeBuffer = new byte[16];
                S7.SetBitAt(ref writeBuffer, 0, 0, true);
                int writeRes = plc.WriteDbRaw(2, 0, writeBuffer);
                if (writeRes == 0)
                    Console.WriteLine("✅ DB Write SUCCESS!");
                else
                    Console.WriteLine($"❌ DB Write FAILED: {writeRes} - {plc.GetErrorText(writeRes)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            plc.Disconnect();
            Console.WriteLine("\n✅ Disconnected");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}