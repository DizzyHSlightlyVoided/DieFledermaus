using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;

namespace DieFledermaus.Tests
{
    class Program
    {
        const int bigBufferLength = 70000;
        [STAThread]
        static void Main(string[] args)
        {
            byte[] bigBuffer = new byte[bigBufferLength];
            for (int i = 0; i < bigBufferLength; i++)
                bigBuffer[i] = (byte)(i + 1);

            byte[] key = new byte[32];
            Random rng = new Random(32);
            rng.NextBytes(key);

            GetMode(bigBuffer, key, MausEncryptionFormat.None);
            Console.WriteLine();
            GetMode(bigBuffer, key, MausEncryptionFormat.Aes);
        }

        private static void GetMode(byte[] bigBuffer, byte[] key, MausEncryptionFormat mode)
        {
            Console.WriteLine("Encryption: " + mode);
            Console.WriteLine("Initializing MemoryStream.");
            using (MemoryStream ms = new MemoryStream())
            {
                using (DieFledermausStream ds = new DieFledermausStream(ms, mode, true))
                using (BinaryWriter writer = new BinaryWriter(ds))
                {
                    if (mode != MausEncryptionFormat.None)
                        SetPasswd(ds);
                    Console.WriteLine("Length before writing 9: " + ms.Length);
                    writer.Write(9);
                    Console.WriteLine("Length after writing 9: " + ms.Length);
                    writer.Write(bigBuffer);
                    Console.WriteLine("Length after writing big array: " + ms.Length);
                }
                Console.WriteLine("Length after DieFledermausStream is closed: " + ms.Length);
                ms.Seek(0, SeekOrigin.Begin);
                Console.WriteLine("Position before constructor: 0");
                using (DieFledermausStream ds = new DieFledermausStream(ms, CompressionMode.Decompress, true))
                using (BinaryReader reader = new BinaryReader(ds))
                {
                    if (mode != MausEncryptionFormat.None)
                        SetPasswd(ds);

                    Console.WriteLine("Position before read: " + ms.Position);
                    Console.WriteLine(reader.ReadInt32());
                    Console.WriteLine("Position during read: " + ms.Position);
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

                    Console.WriteLine("Position after read: " + ms.Position);
                }

                Console.WriteLine("Position after DieFledermausStream is closed: " + ms.Length);
            }

            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }

        private static void SetPasswd(DieFledermausStream ds)
        {
            const string passwd = "Correct Horse!Battery#Staple69105";
            Stopwatch sw = Stopwatch.StartNew();
            ds.SetPassword(passwd);
            sw.Stop();
            Console.WriteLine(" (Time to set password: {0}ms)", sw.Elapsed.TotalMilliseconds);
        }
    }
}
