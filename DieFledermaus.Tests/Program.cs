using System;
using System.Diagnostics;
using System.IO;
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
                using (DieFledermauZArchive archive = new DieFledermauZArchive(ms, MauZArchiveMode.Create, true))
                {
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Deflate, MausEncryptionFormat.None);
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Deflate, MausEncryptionFormat.Aes);
                }
            }
        }

        private static void SetEntry(DieFledermauZArchive archive, byte[] bigBuffer, MausCompressionFormat compFormat, MausEncryptionFormat encFormat)
        {
            var entry = archive.Create(compFormat.ToString() + encFormat.ToString() + ".dat", compFormat, encFormat);

            if (encFormat != MausEncryptionFormat.None)
                SetPasswd(entry);

            using (Stream stream = entry.OpenWrite())
                stream.Write(bigBuffer, 0, bigBufferLength);
        }

        private static void SetPasswd(DieFledermauZItem entry)
        {
            const string passwd = "Correct Horse!Battery#Staple69105";

            Stopwatch sw = Stopwatch.StartNew();
            using (SecureString passwdSS = new SecureString())
            {
                foreach (char c in passwd)
                    passwdSS.AppendChar(c);
                passwdSS.MakeReadOnly();
                entry.SetPassword(passwdSS);
            }
            sw.Stop();
            if (entry.EncryptedOptions != null && !entry.EncryptedOptions.IsReadOnly)
                entry.EncryptedOptions.AddAll();
            Console.WriteLine(" (Time to set password: {0}ms)", sw.Elapsed.TotalMilliseconds);
        }
    }
}
