using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security;

namespace DieFledermaus.Tests
{
    class Program
    {
        const int bigBufferLength = 70000;
        [STAThread]
        static void Main()
        {
            byte[] bigBuffer = new byte[bigBufferLength];
            for (int i = 0; i < bigBufferLength; i++)
                bigBuffer[i] = (byte)(i + 1);

            GetMode(bigBuffer, MausEncryptionFormat.None, MausCompressionFormat.Deflate);
            Console.WriteLine();
            GetMode(bigBuffer, MausEncryptionFormat.None, MausCompressionFormat.Lzma);
            Console.WriteLine();
            GetMode(bigBuffer, MausEncryptionFormat.Aes, MausCompressionFormat.Deflate);
            Console.WriteLine();
            GetMode(bigBuffer, MausEncryptionFormat.None, MausCompressionFormat.None);
        }

        private static void GetMode(byte[] bigBuffer, MausEncryptionFormat mode, MausCompressionFormat format)
        {
            Console.WriteLine("Encryption: " + mode);
            Console.WriteLine("Compression Format: " + format);
            Console.WriteLine("Initializing MemoryStream.");

            using (MemoryStream ms = new MemoryStream())
            {
                Stopwatch sw;
                using (DieFledermausStream ds = new DieFledermausStream(ms, format, mode, true))
                using (BinaryWriter writer = new BinaryWriter(ds))
                {
                    ds.Filename = mode.ToString() + format.ToString();
                    Console.Write("Filename: ");
                    Console.WriteLine(ds.Filename);
                    if (mode != MausEncryptionFormat.None)
                        SetPasswd(ds);
                    writer.Write(9);
                    writer.Write(bigBuffer);
                    sw = Stopwatch.StartNew();
                }
                sw.Stop();
                Console.WriteLine(" (Time to compress: {0}ms)", sw.Elapsed.TotalMilliseconds);
                Console.WriteLine("Length after DieFledermausStream is closed: " + ms.Length);
                ms.Seek(0, SeekOrigin.Begin);
                Console.WriteLine("Position before constructor: 0");
                using (DieFledermausStream ds = new DieFledermausStream(ms, CompressionMode.Decompress, true))
                using (BinaryReader reader = new BinaryReader(ds))
                {
                    if (mode != MausEncryptionFormat.None)
                        SetPasswd(ds);

                    Console.WriteLine("Position before read: " + ms.Position);
                    sw = Stopwatch.StartNew();
                    ds.LoadData();
                    sw.Stop();
                    Console.WriteLine(" (Time to decompress: {0}ms)", sw.Elapsed.TotalMilliseconds);
                    Console.Write("Filename: ");
                    Console.WriteLine(ds.Filename);
                    Console.Write("Read number: ");
                    Console.WriteLine(reader.ReadInt32());
                    byte[] readBigBuffer = reader.ReadBytes(bigBufferLength);
                    Console.WriteLine("Array length: " + readBigBuffer.Length);

                    bool changed = false;

                    if (readBigBuffer.Length >= bigBufferLength)
                    {
                        for (int i = 0; i < bigBufferLength; i++)
                        {
                            if (bigBuffer[i] != readBigBuffer[i])
                            {
                                Console.WriteLine("Mismatch starting at index " + i);
                                changed = true;
                                break;
                            }
                        }
                    }
                    if (!changed) Console.WriteLine("Array is also the same!");

                }
            }

            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }

        private static void SetPasswd(DieFledermausStream ds)
        {
            const string passwd = "Correct Horse!Battery#Staple69105";

            Stopwatch sw = Stopwatch.StartNew();
            using (SecureString passwdSS = new SecureString())
            {
                foreach (char c in passwd)
                    passwdSS.AppendChar(c);
                ds.SetPassword(passwdSS);
            }
            sw.Stop();
            if (ds.EncryptedOptions != null && ds.CanWrite)
                ds.EncryptedOptions.AddAll();
            Console.WriteLine(" (Time to set password: {0}ms)", sw.Elapsed.TotalMilliseconds);
        }
    }
}
