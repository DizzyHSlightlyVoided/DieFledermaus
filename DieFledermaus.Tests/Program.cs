using System;
using System.IO;
using System.IO.Compression;

namespace DieFledermaus.Tests
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            const int bigBufferLength = 70000;
            byte[] bigBuffer = new byte[bigBufferLength];
            for (int i = 0; i < bigBufferLength; i++)
                bigBuffer[i] = (byte)(i + 1);

            Console.WriteLine("Initializing MemoryStream.");
            using (MemoryStream ms = new MemoryStream())
            {
                using (DieFledermausStream ds = new DieFledermausStream(ms, CompressionMode.Compress, true))
                using (BinaryWriter writer = new BinaryWriter(ds))
                {
                    Console.WriteLine("Length before writing 9: " + ms.Length);
                    writer.Write(9);
                    Console.WriteLine("Length after writing 9: " + ms.Length);
                    writer.Write(bigBuffer);
                    Console.WriteLine("Length after writing big array: " + ms.Length);
                }
                Console.WriteLine("Length after DieFledermausStream is closed: " + ms.Length);
                ms.Seek(0, SeekOrigin.Begin);
                using (DieFledermausStream ds = new DieFledermausStream(ms, CompressionMode.Decompress, true))
                using (BinaryReader reader = new BinaryReader(ds))
                {
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
    }
}
