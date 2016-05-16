using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;

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
                }
                {
                    var keyPair = GetKeyPair(Keys.Encryption);
                    publicKeyEnc = (RsaKeyParameters)keyPair.Public;
                    privateKeyEnc = (RsaKeyParameters)keyPair.Private;
                }
                ECKeyParameters publicKeyECDSA, privateKeyECDSA;
                {
                    var keyPair = GetKeyPair(Keys.ECDSA);
                    publicKeyECDSA = (ECKeyParameters)keyPair.Public;
                    privateKeyECDSA = (ECKeyParameters)keyPair.Private;
                }

                Console.WriteLine("Creating archive ...");
                Stopwatch sw;
                using (DieFledermauZArchive archive = new DieFledermauZArchive(ms, MauZArchiveMode.Create, true))
                {
                    archive.RSASignParameters = archive.DefaultRSASignParameters = privateKeySig;
                    archive.RSASignId = archive.DefaultRSASignId = "RSA";
                    archive.ECDSASignParameters = archive.DefaultECDSASignParameters = privateKeyECDSA;
                    archive.ECDSASignId = archive.DefaultECDSASignId = "ECDSA";
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Deflate, MausEncryptionFormat.None, publicKeyEnc);
                    SetEntry(archive, bigBuffer, MausCompressionFormat.Lzma, MausEncryptionFormat.Aes, publicKeyEnc);
                    SetEntry(archive, bigBuffer, MausCompressionFormat.None, MausEncryptionFormat.Threefish, publicKeyEnc);
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
                    archive.RSASignParameters = publicKeySig;
                    if (archive.VerifyRSASignature())
                        Console.WriteLine("RSA signature verified for the archive.");
                    else
                        Console.WriteLine("RSA signature NOT verified for the archive!");
                    archive.ECDSASignParameters = publicKeyECDSA;
                    if (archive.VerifyECDSASignature())
                        Console.WriteLine("ECDSA signature verified for the archive.");
                    else
                        Console.WriteLine("ECDSA signature NOT verified for the archive!");

                    Console.WriteLine("Completed reading and signature-verification in {0}ms", sw.Elapsed.TotalMilliseconds);

                    foreach (DieFledermauZItem item in archive.Entries.ToArray().Where(i => i.EncryptionFormat != MausEncryptionFormat.None))
                    {
                        Console.WriteLine(" - Decrypting archive entry ...");
                        if (item.IsRSAEncrypted)
                            item.RSAEncryptParameters = privateKeyEnc;
                        else
                            SetPasswd(item);

                        sw = Stopwatch.StartNew();
                        var dItem = item.Decrypt();
                        sw.Stop();
                        Console.WriteLine("Successfully decrypted {0} in {1}ms", dItem.Path, sw.Elapsed.TotalMilliseconds);
                    }

                    foreach (DieFledermauZArchiveEntry entry in archive.Entries.OfType<DieFledermauZArchiveEntry>())
                    {
                        entry.RSASignParameters = publicKeySig;
                        entry.ECDSASignParameters = publicKeyECDSA;

                        Console.WriteLine(" - Reading file: " + entry.Path);
                        Console.WriteLine("Hash: " + GetString(entry.ComputeHash()));
                        if (entry.VerifyRSASignature())
                            Console.WriteLine("RSA signature is verified.");
                        else
                            Console.WriteLine("RSA signature is NOT verified!");
                        if (entry.VerifyECDSASignature())
                            Console.WriteLine("ECDSA signature is verified.");
                        else
                            Console.WriteLine("ECDSA signature is NOT verified!");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

        private static AsymmetricCipherKeyPair GetKeyPair(string pem)
        {
            using (StringReader sr = new StringReader(pem))
            {
                PemReader reader = new PemReader(sr);
                return (AsymmetricCipherKeyPair)reader.ReadObject();
            }
        }

        private static void SetEntry(DieFledermauZArchive archive, byte[] bigBuffer, MausCompressionFormat compFormat, MausEncryptionFormat encFormat,
            RsaKeyParameters encKey)
        {
            var entry = archive.Create("Files/" + compFormat.ToString() + encFormat.ToString() + ".dat", compFormat, encFormat);

            Console.WriteLine(" - Building file: " + entry.Path);

            if (encFormat != MausEncryptionFormat.None)
                entry.RSAEncryptParameters = encKey;

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
        }

        private static string GetString(byte[] hash)
        {
            return string.Concat(hash.Select(i => i.ToString("x2", System.Globalization.NumberFormatInfo.InvariantInfo)).ToArray());
        }
    }
}
