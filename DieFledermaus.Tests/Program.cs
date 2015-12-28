using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace DieFledermaus.Tests
{
    internal static class Program
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
                RsaKeyParameters publicKeySig, privateKeySig, publicKeyEnc, privateKeyEnc;

                {
                    var keyPair = GetKeyPair(Keys.Signature);
                    publicKeySig = (RsaKeyParameters)keyPair.Public;
                    privateKeySig = (RsaKeyParameters)keyPair.Private;

                    keyPair = GetKeyPair(Keys.Encryption);

                    publicKeyEnc = (RsaKeyParameters)keyPair.Public;
                    privateKeyEnc = (RsaKeyParameters)keyPair.Private;
                }

                Console.WriteLine("Creating archive ...");
                Stopwatch sw;
                using (DieFledermauZArchive archive = new DieFledermauZArchive(ms, MauZArchiveMode.Create, true))
                {
                    archive.RSASignParameters = privateKeySig;
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Deflate, MausEncryptionFormat.None);
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Lzma, MausEncryptionFormat.Aes);
                    SetEntry(archive, bigBuffer, MausCompressionFormat.None, MausEncryptionFormat.Threefish);
                    Console.WriteLine(" - Building empty directory: EmptyDir/");
                    var emptyDir = archive.AddEmptyDirectory("EmptyDir/", MausEncryptionFormat.Twofish);
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

                    foreach (DieFledermauZArchiveEntry entry in archive.Entries.OfType<DieFledermauZArchiveEntry>())
                    {
                        entry.RSASignParameters = publicKeySig;
                        entry.VerifyRSASignature();

                        Console.WriteLine(" - Reading file: " + entry.Path);
                        Console.WriteLine("Hash: " + GetString(entry.ComputeHash()));
                        if (entry.IsRSASignVerified)
                            Console.WriteLine("RSA signature is verified.");
                        else
                            Console.WriteLine("RSA signature is NOT verified!");
                    }
                }
            }

            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

        private static AsymmetricCipherKeyPair GetKeyPair(string xml)
        {
            XElement root = XDocument.Parse(xml).Root;

            RsaKeyParameters publicKey = new RsaKeyParameters(false, root.GetBigInt("Modulus"), root.GetBigInt("Exponent"));
            RsaKeyParameters privateKey = new RsaPrivateCrtKeyParameters(publicKey.Modulus, publicKey.Exponent, root.GetBigInt("D"),
                  root.GetBigInt("P"), root.GetBigInt("Q"), root.GetBigInt("DP"), root.GetBigInt("DQ"), root.GetBigInt("InverseQ"));

            return new AsymmetricCipherKeyPair(publicKey, privateKey);
        }

        public static BigInteger GetBigInt(this XElement node, XName name)
        {
            return new BigInteger(1, Convert.FromBase64String(node.Element(name).Value));
        }

        private static void SetEntry(DieFledermauZArchive archive, byte[] bigBuffer, MausCompressionFormat compFormat, MausEncryptionFormat encFormat)
        {
            var entry = archive.Create("Files/" + compFormat.ToString() + encFormat.ToString() + ".dat", compFormat, encFormat);

            Console.WriteLine(" - Building file: " + entry.Path);

            if (encFormat != MausEncryptionFormat.None)
            {
                SetPasswd(entry);
            }

            using (Stream stream = entry.OpenWrite())
                stream.Write(bigBuffer, 0, bigBufferLength);

            Console.WriteLine("Hash: " + GetString(entry.ComputeHash()));
        }

        private static void SetPasswd(DieFledermauZItem item)
        {
            const string passwd = "Correct Horse!Battery#Staple69105";

            item.Password = passwd;
            Stopwatch sw = Stopwatch.StartNew();
            item.DeriveKey();
            sw.Stop();
            Console.WriteLine("Time to derive key: {0}ms", sw.Elapsed.TotalMilliseconds);

            DieFledermauZArchiveEntry entry = item as DieFledermauZArchiveEntry;
            if (entry != null && entry.EncryptedOptions != null && !entry.EncryptedOptions.IsReadOnly)
                entry.EncryptedOptions.AddAll();
        }

        private static string GetString(byte[] hash)
        {
            return string.Concat(hash.Select(i => i.ToString("x2", System.Globalization.NumberFormatInfo.InvariantInfo)).ToArray());
        }
    }
}
