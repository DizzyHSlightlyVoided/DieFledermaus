using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            using (MemoryStream ms = new MemoryStream())
            {
                Console.WriteLine("Creating archive ...");
                Stopwatch sw;
                using (DieFledermauZArchive archive = new DieFledermauZArchive(ms, MauZArchiveMode.Create, true))
                {
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Deflate, MausEncryptionFormat.None);
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Lzma, MausEncryptionFormat.Aes);
                    Console.WriteLine(" - Building empty directory: EmptyDir/");
                    var emptyDir = archive.AddEmptyDirectory("EmptyDir/");
                    emptyDir.EncryptPath = true;
                    SetPasswd(emptyDir);
                    Console.WriteLine("Compressing archive ...");
                    sw = Stopwatch.StartNew();
                }

                sw.Stop();
                Console.WriteLine("Completed compression in {0}ms", sw.Elapsed.TotalMilliseconds);
                Console.WriteLine();
                Console.WriteLine("Decoding archive ...");

                using (DieFledermauZArchive archive = new DieFledermauZArchive(ms, MauZArchiveMode.Read, true))
                {
                    foreach (DieFledermauZItem item in archive.Entries.ToArray().Where(i => i.EncryptionFormat != MausEncryptionFormat.None))
                    {
                        Console.WriteLine(" - Decrypting file ...");
                        SetPasswd(item);
                        sw = Stopwatch.StartNew();
                        var dItem = item.Decrypt();
                        sw.Stop();
                        Console.WriteLine("Successfully decrypted {0} in {1}ms", dItem.Path, sw.Elapsed.TotalMilliseconds);
                    }

                    byte[] getBuffer = new byte[bigBufferLength];

                    foreach (DieFledermauZArchiveEntry entry in archive.Entries.Where(i => i is DieFledermauZArchiveEntry))
                    {
                        Console.WriteLine(" - Reading file: " + entry.Path);
                        using (Stream outStream = entry.OpenRead())
                        {
                            int read = outStream.Read(getBuffer, 0, bigBufferLength);

                            if (read == bigBufferLength)
                                Console.WriteLine("Correct length!");
                            else
                                Console.WriteLine("Wrong length: " + read);

                            bool correct = true;

                            for (int i = 0; correct && i < read; i++)
                            {
                                if (getBuffer[i] != bigBuffer[i])
                                {
                                    Console.WriteLine("Wrong value!");
                                    correct = false;
                                }
                            }
                            if (correct)
                                Console.WriteLine("All values are correct as well!");
                        }
                    }
                }
            }

            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

        private static void SetEntry(DieFledermauZArchive archive, byte[] bigBuffer, MausCompressionFormat compFormat, MausEncryptionFormat encFormat)
        {
            var entry = archive.Create("Files/" + compFormat.ToString() + encFormat.ToString() + ".dat", compFormat, encFormat);

            Console.WriteLine(" - Building file: " + entry.Path);

            if (encFormat != MausEncryptionFormat.None)
                SetPasswd(entry);

            using (Stream stream = entry.OpenWrite())
                stream.Write(bigBuffer, 0, bigBufferLength);
        }

        private static void SetPasswd(DieFledermauZItem item)
        {
            const string passwd = "Correct Horse!Battery#Staple69105";

            if (item.Archive.Mode == MauZArchiveMode.Create)
            {
                SecureString passwdSS = new SecureString();
                {
                    foreach (char c in passwd)
                        passwdSS.AppendChar(c);
                    passwdSS.MakeReadOnly();

                    item.Password = passwdSS;
                }
            }
            else
            {
                item.SetPassword(passwd);
            }

            DieFledermauZArchiveEntry entry = item as DieFledermauZArchiveEntry;
            if (entry != null && entry.EncryptedOptions != null && !entry.EncryptedOptions.IsReadOnly)
                entry.EncryptedOptions.AddAll();
        }
    }
}
