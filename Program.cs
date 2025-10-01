using Sharp7;

Console.WriteLine("=== S7-1500 PLC Connection Test ===\n");

// PLC Ayarları
string plcIP = "192.168.0.10";  // ← PLC'nizin gerçek IP'sini yazın
int rack = 0;
int slot = 1;

var client = new S7Client();

Console.WriteLine($"Connecting to PLC...");
Console.WriteLine($"  IP: {plcIP}");
Console.WriteLine($"  Rack: {rack}");
Console.WriteLine($"  Slot: {slot}\n");

// Bağlan
int result = client.ConnectTo(plcIP, rack, slot);

if (result == 0)
{
    Console.WriteLine("✅ PLC CONNECTED!");
    Console.WriteLine($"   Connected: {client.Connected}\n");

    // DB1 okuma testi
    Console.WriteLine("Testing DB1 Read (32 bytes)...");
    byte[] buffer = new byte[32];
    result = client.DBRead(1, 0, 32, buffer);

    if (result == 0)
    {
        Console.WriteLine("✅ DB Read SUCCESS!");
        Console.WriteLine("\nFirst 32 bytes (HEX):");
        Console.WriteLine(BitConverter.ToString(buffer));

        Console.WriteLine("\nFirst 32 bytes (DEC):");
        for (int i = 0; i < buffer.Length; i++)
        {
            Console.Write($"[{i}]={buffer[i]:D3} ");
            if ((i + 1) % 8 == 0) Console.WriteLine();
        }

        // Bit okuma testi
        Console.WriteLine("\n--- Bit Tests ---");
        Console.WriteLine($"Byte[0], Bit[0] (APK1_Okundu): {S7.GetBitAt(buffer, 0, 0)}");
        Console.WriteLine($"Byte[10], Bit[0] (APK2_Okundu): {S7.GetBitAt(buffer, 10, 0)}");
        Console.WriteLine($"Byte[20], Bit[1] (Koli_Sort_OK): {S7.GetBitAt(buffer, 20, 1)}");

        // String okuma testi
        Console.WriteLine("\n--- String Tests ---");
        string mfc1 = S7.GetCharsAt(buffer, 2, 8);
        string mfc2 = S7.GetCharsAt(buffer, 12, 8);
        Console.WriteLine($"MFC1 (Byte 2-9): '{mfc1}'");
        Console.WriteLine($"MFC2 (Byte 12-19): '{mfc2}'");
    }
    else
    {
        Console.WriteLine($"❌ DB Read FAILED!");
        Console.WriteLine($"   Error Code: {result} (0x{result:X8})");
        Console.WriteLine($"   Error Text: {client.ErrorText(result)}");
    }

    // DB2 yazma testi
    Console.WriteLine("\n\nTesting DB2 Write (Heartbeat)...");
    byte[] writeBuffer = new byte[31];
    S7.SetBitAt(ref writeBuffer, 30, 1, true); // HAB_OK = TRUE

    result = client.DBWrite(2, 0, writeBuffer.Length, writeBuffer);

    if (result == 0)
    {
        Console.WriteLine("✅ DB Write SUCCESS!");
    }
    else
    {
        Console.WriteLine($"❌ DB Write FAILED!");
        Console.WriteLine($"   Error Code: {result} (0x{result:X8})");
        Console.WriteLine($"   Error Text: {client.ErrorText(result)}");
    }

    client.Disconnect();
    Console.WriteLine("\n✅ Disconnected");
}
else
{
    Console.WriteLine($"❌ CONNECTION FAILED!");
    Console.WriteLine($"   Error Code: {result} (0x{result:X8})");
    Console.WriteLine($"   Error Text: {client.ErrorText(result)}");
    Console.WriteLine("\nPossible reasons:");
    Console.WriteLine("  - Wrong IP address");
    Console.WriteLine("  - Wrong Rack/Slot (try Slot=2 for S7-300)");
    Console.WriteLine("  - Firewall blocking port 102");
    Console.WriteLine("  - PLC CPU stopped or not accessible");
    Console.WriteLine("  - Network cable unplugged");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();