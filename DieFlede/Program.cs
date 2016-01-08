#region BSD license
/*
Copyright © 2015, KimikoMuffin.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. The names of its contributors may not be used to endorse or promote 
   products derived from this software without specific prior written 
   permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using DieFledermaus.Cli.Globalization;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;

namespace DieFledermaus.Cli
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Console.Write(TextResources.Title);
            Console.Write(' ');
            Console.WriteLine(typeof(Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyFileVersionAttribute), false)
                .OfType<System.Reflection.AssemblyFileVersionAttribute>().FirstOrDefault().Version);
            Console.WriteLine();
            Console.WriteLine(TextResources.Disclaimer);
            Console.WriteLine();

            ClParser parser = new ClParser();
            ClParamFlag create = new ClParamFlag(parser, TextResources.HelpMCreate, 'c', "create", TextResources.PNameCreate);
            ClParamFlag extract = new ClParamFlag(parser, TextResources.HelpMExtract, 'x', "extract", TextResources.PNameExtract);
            ClParamFlag list = new ClParamFlag(parser, TextResources.HelpMList, 'l', "list", TextResources.PNameList);
            ClParamFlag help = new ClParamFlag(parser, TextResources.HelpMHelp, 'h', "help", TextResources.PNameHelp);
            extract.MutualExclusives.Add(create);
            create.MutualExclusives.Add(extract);
            create.MutualExclusives.Add(list);
            create.OtherMessages.Add(list, NoOutputCreate);

            ClParamEnum<MausCompressionFormat> cFormat;
            {
                Dictionary<string, MausCompressionFormat> locArgs = new Dictionary<string, MausCompressionFormat>();
                locArgs.Add("DEFLATE", MausCompressionFormat.Deflate);
                locArgs.Add("LZMA", MausCompressionFormat.Lzma);
                locArgs.Add(TextResources.PFormatNone, MausCompressionFormat.None);

                Dictionary<string, MausCompressionFormat> unArgs = new Dictionary<string, MausCompressionFormat>() { { "none", MausCompressionFormat.None } };

                cFormat = new ClParamEnum<MausCompressionFormat>(parser, TextResources.HelpMFormat, locArgs, unArgs, '\0', "format", TextResources.PNameFormat);
                cFormat.MutualExclusives.Add(extract);
                cFormat.MutualExclusives.Add(list);
                cFormat.OtherMessages.Add(extract, NoEntryExtract);
                cFormat.OtherMessages.Add(list, NoEntryExtract);
            }

            ClParamFlag single = new ClParamFlag(parser, TextResources.HelpMSingle, 'n', "single", TextResources.PNameSingle);

            ClParamFlag interactive = new ClParamFlag(parser, TextResources.HelpMInteractive, 'i', "interactive", TextResources.PNameInteractive);

            ClParamFlag overwrite = new ClParamFlag(parser, TextResources.HelpMOverwrite, 'w', "overWrite", TextResources.PNameOverwrite);
            ClParamFlag skipexist = new ClParamFlag(parser, TextResources.HelpMSkip, 's', "skip", "skip-existing",
                TextResources.PNameSkip, TextResources.PNameSkipExisting);
            skipexist.MutualExclusives.Add(overwrite);

            ClParamFlag verbose = new ClParamFlag(parser, TextResources.HelpMVerbose, 'v', "verbose", TextResources.PNameVerbose);

            ClParamValue archiveFile = new ClParamValue(parser, TextResources.HelpMArchive, TextResources.HelpArchive, 'f', "file", "archive",
                TextResources.PNameFile, TextResources.PNameArchive);
            archiveFile.ConvertValue = Path.GetFullPath;

            ClParamMulti entryFile = new ClParamMulti(parser, string.Join(Environment.NewLine, TextResources.HelpMEntry, TextResources.HelpMEntry2),
                TextResources.HelpInput, 'e');
            parser.RawParam = entryFile;

            ClParamValue outFile = new ClParamValue(parser, TextResources.HelpMOut, TextResources.HelpOutput, 'o', "out", "output",
                TextResources.PNameOut, TextResources.PNameOutput);
            outFile.ConvertValue = Path.GetFullPath;
            outFile.MutualExclusives.Add(create);
            create.OtherMessages.Add(outFile, NoOutputCreate);

            outFile.MutualExclusives.Add(entryFile);
            entryFile.MutualExclusives.Add(outFile);

            ClParamEnum<MausHashFunction> hash;
            {
                Dictionary<string, MausHashFunction> locArgs =
                    ((MausHashFunction[])Enum.GetValues(typeof(MausHashFunction))).ToDictionary(i => i.ToString());
                hash = new ClParamEnum<MausHashFunction>(parser, TextResources.HelpMHash, locArgs, new Dictionary<string, MausHashFunction>(), 'H',
                    "hash", "hash-funcs", TextResources.PNameHash, TextResources.PNameHashFunc);
            }

            ClParamEnum<MausEncryptionFormat> cEncFmt;
            {
                Dictionary<string, MausEncryptionFormat> locArgs = new Dictionary<string, MausEncryptionFormat>()
                {
                    { "AES", MausEncryptionFormat.Aes },
                    { "Twofish", MausEncryptionFormat.Twofish },
                    { "Threefish", MausEncryptionFormat.Threefish }
                };
                cEncFmt = new ClParamEnum<MausEncryptionFormat>(parser, TextResources.HelpMEncFmt, locArgs,
                    new Dictionary<string, MausEncryptionFormat>(), '\0', "encryption", TextResources.PNameEncFmt);
            }

            ClParamFlag hide = new ClParamFlag(parser, TextResources.HelpMHide, '\0', "hide", TextResources.PNameHide);
            extract.MutualExclusives.Add(hide);
            extract.OtherMessages.Add(hide, NoEntryExtract);

            ClParamValue sigKey = new ClParamValue(parser, TextResources.HelpMSigKey, TextResources.HelpPath, '\0', "signature-key", TextResources.PNameSigKey);
            ClParamValue sigDex = new ClParamValue(parser, TextResources.HelpMSigDex, TextResources.HelpIndex, '\0', "signature-index", TextResources.PNameSigDex);

            ClParam[] clParams = parser.Params.ToArray();

            if (args.Length == 1 && args[0][0] != '-')
            {
                archiveFile.IsSet = true;
                archiveFile.Value = args[0];
                extract.IsSet = true;
            }
            else if (parser.Parse(args))
            {
                ShowHelp(clParams, false);
                return Return(-1, interactive);
            }
            bool acting = false;

            if (args.Length > 0)
            {
                if (extract.IsSet || list.IsSet)
                {
                    if (!archiveFile.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.ExtractNoArchive);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }
                    else if (!File.Exists(archiveFile.Value))
                    {
                        Console.Error.WriteLine(TextResources.FileNotFound, archiveFile.Value);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }

                    if (extract.IsSet)
                    {
                        if (outFile.IsSet && !Directory.Exists(outFile.Value))
                        {
                            Console.Error.WriteLine(TextResources.DirNotFound, outFile.Value);
                            ShowHelp(clParams, false);
                            return Return(-1, interactive);
                        }
                    }
                    else if (outFile.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.OutDirOnly);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }

                    acting = true;
                }
                else if (create.IsSet)
                {
                    if (!entryFile.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.CreateNoEntry);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }

                    if (!single.IsSet && !archiveFile.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.ExtractNoArchive);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }

                    if (cEncFmt.IsSet)
                    {
                        if (!interactive.IsSet)
                        {
                            Console.Error.WriteLine(TextResources.EncryptionNoOpts);
                            ShowHelp(clParams, false);
                            return -1;
                        }
                    }
                    else if (hide.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.ErrorHide, hide.Key);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }
                    acting = true;
                }
                else if (archiveFile.IsSet || entryFile.IsSet || outFile.IsSet)
                {
                    Console.Error.WriteLine(TextResources.RequireAtLeastOne, "-c, -x, -l");
                    ShowHelp(clParams, false);
                    return Return(-1, interactive);
                }
                else if (!help.IsSet)
                {
                    Console.Error.WriteLine(TextResources.RequireAtLeastOne, "-c, -x, -l, --help");
                    ShowHelp(clParams, false);
                    return Return(-1, interactive);
                }
            }

            if ((help.IsSet && !acting) || args.Length == 0)
            {
                bool showFull = help.IsSet && !acting;

                ShowHelp(clParams, showFull);
                if (showFull)
                    return Return(0, interactive);
            }

            AsymmetricKeyParameter keyObj;

            if (sigKey.IsSet)
            {
                string path = sigKey.Value;
                BigInteger index;

                if (sigDex.IsSet)
                {
                    try
                    {
                        index = new BigInteger(sigDex.Value);
                        if (index.CompareTo(BigInteger.Zero) < 0)
                            throw new InvalidDataException();
                    }
                    catch
                    {
                        Console.Error.WriteLine(TextResources.BadInteger, sigDex.Key, sigDex.Value);
                        return Return(-7, interactive);
                    }
                }
                else index = null;

                keyObj = LoadKeyFile(path, index, create.IsSet, interactive);

                if (keyObj == null)
                    return Return(-7, interactive);
            }
            else if (sigDex.IsSet)
            {
                Console.Error.WriteLine(TextResources.SigDexNeedsKey);
                return Return(-7, interactive);
            }
            else keyObj = null;

            string ssPassword = null;
            List<FileStream> streams = null;
            const string mausExt = ".maus";
            const int mausExtLen = 5;
            try
            {
                if (create.IsSet)
                {
                    #region Create - Single
                    if (single.IsSet)
                    {
                        string entry = Path.GetFullPath(entryFile.Values[0]);
                        FileInfo entryInfo = new FileInfo(entry);
                        using (FileStream fs = File.OpenRead(entry))
                        {
                            MausCompressionFormat compFormat = cFormat.Value.HasValue ? cFormat.Value.Value : MausCompressionFormat.Deflate;
                            MausEncryptionFormat encFormat = MausEncryptionFormat.None;

                            if (OverwritePrompt(interactive, overwrite, skipexist, verbose, ref archiveFile.Value))
                                return -3;

                            if (CreateEncrypted(cEncFmt, out encFormat, out ssPassword))
                                return -4;

                            if (archiveFile.Value == null)
                                archiveFile.Value = entry + mausExt;

                            if (archiveFile.Value.Equals(entry, StringComparison.Ordinal))
                            {
                                Console.WriteLine(TextResources.OverwriteSameEntry, entry);
                                return Return(-3, interactive);
                            }

                            using (Stream arStream = File.Create(archiveFile.Value))
                            using (DieFledermausStream ds = new DieFledermausStream(arStream, compFormat, encFormat))
                            {
                                ds.RSASignParameters = keyObj as RsaKeyParameters;
                                ds.DSASignParameters = keyObj as DsaKeyParameters;
                                ds.ECDSASignParameters = keyObj as ECKeyParameters;

                                if (hash.Value.HasValue)
                                    ds.HashFunction = hash.Value.Value;

                                if (ssPassword != null)
                                    ds.Password = ssPassword;

                                try
                                {
                                    ds.CreatedTime = entryInfo.CreationTimeUtc;
                                }
                                catch
                                {
                                    if (verbose.IsSet)
                                        Console.WriteLine(TextResources.TimeCGet, entryInfo.FullName);
                                }

                                try
                                {
                                    ds.ModifiedTime = entryInfo.LastWriteTimeUtc;
                                }
                                catch
                                {
                                    if (verbose.IsSet)
                                        Console.WriteLine(TextResources.TimeMGet, entryInfo.FullName);
                                }

                                ds.Filename = Path.GetFileName(entry);
                                fs.CopyTo(ds);
                            }

                            if (verbose.IsSet)
                                Console.WriteLine(TextResources.Completed);
                            return Return(0, interactive);
                        }
                    }
                    #endregion
                    #region Create - Archive
                    else
                    {
                        MausCompressionFormat compFormat = cFormat.Value.HasValue ? cFormat.Value.Value : MausCompressionFormat.Deflate;
                        streams = new List<FileStream>(entryFile.Count);
                        List<FileInfo> fileInfos = new List<FileInfo>(streams.Capacity);

                        var entries = entryFile.Values.Select(Path.GetFullPath).Distinct().ToArray();

                        for (int i = 0; i < entries.Length; i++)
                        {
                            var curEntry = entries[i];
                            var curInfo = new FileInfo(curEntry);
                            if (!curInfo.Exists)
                            {
                                Console.Error.WriteLine(TextResources.FileNotFound, curEntry);
                                continue;
                            }

                            try
                            {
                                streams.Add(File.OpenRead(curEntry));
                                fileInfos.Add(curInfo);
                            }
                            catch (Exception e)
                            {
                                Console.Error.WriteLine(e.Message);
#if DEBUG
                                GoThrow(e);
#endif
                            }
                        }
                        if (streams.Count == 0)
                        {
                            Console.Error.WriteLine(TextResources.NoEntriesCreate);
                            return Return(-1, interactive);
                        }

                        if (OverwritePrompt(interactive, overwrite, skipexist, verbose, ref archiveFile.Value))
                            return -3;

                        MausEncryptionFormat encFormat = MausEncryptionFormat.None;
                        if (CreateEncrypted(cEncFmt, out encFormat, out ssPassword))
                            return -4;

                        using (FileStream fs = File.OpenWrite(archiveFile.Value))
                        using (DieFledermauZArchive archive = new DieFledermauZArchive(fs, hide.IsSet ? encFormat : MausEncryptionFormat.None))
                        {
                            archive.RSASignParameters = archive.DefaultRSASignParameters = keyObj as RsaKeyParameters;
                            archive.DSASignParameters = archive.DefaultDSASignParameters = keyObj as DsaKeyParameters;
                            archive.ECDSASignParameters = archive.DefaultECDSASignParameters = keyObj as ECKeyParameters;

                            if (hash.Value.HasValue)
                                archive.HashFunction = hash.Value.Value;
                            if (hide.IsSet)
                                archive.Password = ssPassword;

                            for (int i = 0; i < fileInfos.Count; i++)
                            {
                                var curInfo = fileInfos[i];

                                DieFledermauZArchiveEntry entry = archive.Create(curInfo.Name, compFormat, hide.IsSet ? MausEncryptionFormat.None : encFormat);

                                if (verbose.IsSet)
                                    entry.Progress += Entry_Progress;

                                if (cEncFmt.IsSet && !hide.IsSet)
                                    entry.Password = ssPassword;

                                using (Stream writeStream = entry.OpenWrite())
                                    streams[i].CopyTo(writeStream);

                                try
                                {
                                    entry.CreatedTime = curInfo.CreationTimeUtc;
                                }
                                catch
                                {
                                    if (verbose.IsSet)
                                        Console.Error.WriteLine(TextResources.TimeCGet, curInfo.FullName);
                                }
                                try
                                {
                                    entry.ModifiedTime = curInfo.LastWriteTimeUtc;
                                }
                                catch
                                {
                                    if (verbose.IsSet)
                                        Console.Error.WriteLine(TextResources.TimeMGet, curInfo.FullName);
                                }
                            }
                            if (verbose.IsSet)
                                Console.WriteLine();
                        }

                        if (verbose.IsSet)
                            Console.WriteLine(TextResources.Completed);
                        return Return(0, interactive);
                    }
                    #endregion
                }

                #region Extract/List
                using (FileStream fs = File.OpenRead(archiveFile.Value))
                using (DieFledermauZArchive dz = new DieFledermauZArchive(fs, MauZArchiveMode.Read))
                {
                    if (dz.EncryptionFormat != MausEncryptionFormat.None)
                    {
                        if (!interactive.IsSet)
                        {
                            Console.Error.WriteLine(TextResources.EncryptedEx);
                            Console.Error.WriteLine(TextResources.EncryptionNoOptsEx);
                            return -4;
                        }

                        Console.WriteLine(TextResources.EncryptedEx);

                        if (EncryptionPrompt(dz, dz.EncryptionFormat, out ssPassword))
                            return -4;
                    }

                    #region Archive Signature
                    if (dz.IsRSASigned && keyObj is RsaKeyParameters)
                    {
                        dz.RSASignParameters = (RsaKeyParameters)keyObj;
                        if (dz.VerifyRSASignature())
                            Console.WriteLine(TextResources.SignRSAArchiveVerified);
                        else
                            Console.WriteLine(TextResources.SignRSAArchiveUnverified);
                        Console.WriteLine();
                    }

                    if (dz.IsDSASigned && keyObj is DsaKeyParameters)
                    {
                        dz.DSASignParameters = (DsaKeyParameters)keyObj;
                        if (dz.VerifyDSASignature())
                            Console.WriteLine(TextResources.SignDSAArchiveVerified);
                        else
                            Console.WriteLine(TextResources.SignDSAArchiveUnverified);
                        Console.WriteLine();
                    }

                    if (dz.IsECDSASigned && keyObj is ECKeyParameters)
                    {
                        dz.ECDSASignParameters = keyObj as ECKeyParameters;
                        if (dz.VerifyECDSASignature())
                            Console.WriteLine(TextResources.SignECDSAArchiveVerified);
                        else
                            Console.WriteLine(TextResources.SignECDSAArchiveUnverified);
                        Console.WriteLine();
                    }
                    #endregion

                    Regex[] matches;

                    if (entryFile.IsSet)
                        matches = entryFile.Values.Select(GetRegex).ToArray();
                    else
                        matches = null;

                    #region List
                    if (list.IsSet)
                    {
                        for (int i = 0; i < dz.Entries.Count; i++)
                        {
                            var curEntry = dz.Entries[i];
                            if (curEntry.Path == "/Manifest.dat")
                                continue;

                            if (DoFailDecrypt(curEntry, interactive, i, ref ssPassword) || !MatchesRegexAny(matches, curEntry.Path))
                            {
                                Console.WriteLine(GetName(i, curEntry));
                                continue;
                            }

                            Console.WriteLine(curEntry.Path);

                            VerifySigns(keyObj, curEntry as DieFledermauZArchiveEntry);

                            if (verbose.IsSet)
                            {
                                var curItem = curEntry as DieFledermauZArchiveEntry;
                                if (curItem == null) continue;
                                if (curItem.CreatedTime.HasValue)
                                {
                                    Console.Write(" ");
                                    Console.Write(string.Format(TextResources.TimeC, curItem.CreatedTime.Value));

                                    if (curItem.ModifiedTime.HasValue)
                                        Console.Write(" / ");
                                    else
                                        Console.WriteLine();
                                }
                                if (curItem.ModifiedTime.HasValue)
                                {
                                    if (!curItem.CreatedTime.HasValue)
                                        Console.Write(' ');
                                    Console.WriteLine(TextResources.TimeM, curItem.ModifiedTime.Value);
                                }

                                if (!string.IsNullOrWhiteSpace(curItem.Comment))
                                {
                                    Console.WriteLine(" " + TextResources.Comment);
                                    Console.WriteLine(curItem.Comment.Trim());
                                }
                            }
                            Console.WriteLine();
                        }
                    }
                    #endregion

                    #region Extract
                    if (extract.IsSet)
                    {
                        string outDir;
                        if (outFile.IsSet)
                            outDir = outFile.Value;
                        else
                            outDir = Environment.CurrentDirectory;

                        string[] outPaths = new string[dz.Entries.Count];

                        for (int i = 0; i < dz.Entries.Count; i++)
                        {
                            var entry = dz.Entries[i];
                            if (DoFailDecrypt(entry, interactive, i, ref ssPassword))
                                continue;

                            VerifySigns(keyObj, entry as DieFledermauZArchiveEntry);

                            string path = entry.Path;

                            if (path == null) //The only time this will be happen is when decoding a single DieFledermaus file.
                            {
                                if (mausExt.Equals(Path.GetExtension(archiveFile.Value), StringComparison.OrdinalIgnoreCase))
                                {
                                    path = archiveFile.Value.Substring(0, archiveFile.Value.Length - mausExtLen);
                                    if (verbose.IsSet)
                                        Console.WriteLine(TextResources.RenameExtract, path);
                                }
                                else OverwritePrompt(ref path);
                            }
                            outPaths[i] = Path.Combine(outDir, path);
                        }

                        int extracted = 0;

                        for (int i = 0; i < outPaths.Length; i++)
                        {
                            string curPath = outPaths[i];
                            if (curPath == null)
                                continue;

                            try
                            {
                                if (OverwritePrompt(interactive, overwrite, skipexist, verbose, ref curPath))
                                    continue;

                                var curItem = dz.Entries[i];

                                var curDir = curItem as DieFledermauZEmptyDirectory;

                                if (curDir != null)
                                {
                                    if (File.Exists(curPath))
                                        File.Delete(curPath);

                                    if (!Directory.Exists(curPath))
                                        Directory.CreateDirectory(curPath);
                                    extracted++;
                                    continue;
                                }

                                var curEntry = curItem as DieFledermauZArchiveEntry;

                                using (Stream curS = curEntry.OpenWrite())
                                using (FileStream curFS = File.Create(curPath))
                                    curS.CopyTo(curFS);

                                FileInfo fInfo = new FileInfo(curPath);

                                if (curEntry.CreatedTime.HasValue)
                                {
                                    try
                                    {
                                        fInfo.CreationTimeUtc = curEntry.CreatedTime.Value;
                                    }
                                    catch
                                    {
                                        if (verbose.IsSet)
                                            Console.Error.WriteLine(TextResources.TimeCSet, curPath);
                                    }
                                }
                                if (curEntry.ModifiedTime.HasValue)
                                {
                                    try
                                    {
                                        fInfo.LastWriteTimeUtc = curEntry.ModifiedTime.Value;
                                    }
                                    catch
                                    {
                                        if (verbose.IsSet)
                                            Console.Error.WriteLine(TextResources.TimeMSet, curPath);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.Error.WriteLine(e.Message);
#if DEBUG
                                GoThrow(e);
#endif
                                continue;
                            }
                            extracted++;
                        }

                        if (verbose.IsSet)
                            Console.WriteLine(TextResources.SuccessExtract, extracted, dz.Entries.Count - extracted);
                    }
                    #endregion
                }
                #endregion

                if (verbose.IsSet)
                    Console.WriteLine(TextResources.Completed);
                return Return(0, interactive);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
#if DEBUG
                GoThrow(e);
#endif
                return Return(e.HResult, interactive);
            }
            finally
            {
                if (streams != null)
                {
                    for (int i = 0; i < streams.Count; i++)
                        streams[i].Dispose();
                }
            }
        }

        private static void VerifySigns(ICipherParameters keyObj, DieFledermauZArchiveEntry curEntry)
        {
            if (keyObj == null || curEntry == null)
                return;

            try
            {
                RsaKeyParameters rsaParam = keyObj as RsaKeyParameters;
                if (rsaParam != null)
                {
                    if (!curEntry.IsRSASigned)
                        return;

                    curEntry.RSASignParameters = rsaParam;
                    if (curEntry.VerifyRSASignature())
                        Console.WriteLine(TextResources.SignRSAVerified, curEntry.Path);
                    else
                        Console.Error.WriteLine(TextResources.SignRSAUnverified, curEntry.Path);
                    return;
                }

                DsaKeyParameters dsaParam = keyObj as DsaKeyParameters;
                if (dsaParam != null)
                {
                    if (!curEntry.IsDSASigned)
                        return;

                    curEntry.DSASignParameters = dsaParam;
                    if (curEntry.VerifyDSASignature())
                        Console.WriteLine(TextResources.SignDSAVerified, curEntry.Path);
                    else
                        Console.Error.WriteLine(TextResources.SignDSAUnverified, curEntry.Path);
                }

                ECKeyParameters ecdsaParam = keyObj as ECKeyParameters;
                if (ecdsaParam == null || !curEntry.IsECDSASigned) //The first one probably shouldn't happen, but ...
                    return;

                curEntry.ECDSASignParameters = ecdsaParam;
                if (curEntry.VerifyECDSASignature())
                    Console.WriteLine(TextResources.SignECDSAVerified, curEntry.Path);
                else
                    Console.Error.WriteLine(TextResources.SignECDSAUnverified, curEntry.Path);
            }
            catch (Exception x)
            {
                Console.Error.WriteLine(x.Message);
            }
        }

        private const string SpinnyChars = "-\\/";
        private static int SpinnyDex = 0;
        private static bool SpinnyGot = false;

        private static void Entry_Progress(object sender, MausProgressEventArgs e)
        {
            DieFledermauZItem item = (DieFledermauZItem)sender;

            if (e.State == MausProgressState.CompletedWriting)
            {
                SpinnyGot = false;
                Console.WriteLine("\b \b");
                return;
            }

            if (SpinnyGot)
                Console.Write("\b");
            else
            {
                SpinnyGot = true;
                Console.Write(item.Path);
                Console.Write(' ');
            }
            Console.Write(SpinnyChars[SpinnyDex]);
            SpinnyDex++;
            SpinnyDex %= 3;

        }

        private static bool DoFailDecrypt(DieFledermauZItem entry, ClParamFlag interactive, int i, ref string ssPassword)
        {
            if (entry.EncryptionFormat == MausEncryptionFormat.None || entry.IsDecrypted)
                return false;
            if (!interactive.IsSet)
            {
                Console.Error.WriteLine(TextResources.EncryptedExEntry, GetName(i, entry));
                return true;
            }

            if (ssPassword == null)
            {
                Console.WriteLine(TextResources.EncryptedExEntry, GetName(i, entry));
                return EncryptionPrompt(entry, entry.EncryptionFormat, out ssPassword);
            }

            try
            {
                entry.Password = ssPassword;

                entry = entry.Decrypt();
                return false;
            }
            catch (CryptoException)
            {
                Console.WriteLine(TextResources.EncryptedExEntry, GetName(i, entry));
                return EncryptionPrompt(entry, entry.EncryptionFormat, out ssPassword);
            }
        }

        private static string GetName(int i, DieFledermauZItem entry)
        {
            if (entry.Path != null) return entry.Path;

            if (entry.EncryptionFormat == MausEncryptionFormat.None)
                return TextResources.UnnamedFile;

            if (entry is DieFledermauZItemUnknown)
                return string.Format(TextResources.ListEncryptedUnknown, i + 1);

            return string.Format(TextResources.ListEncryptedEntry, i + 1);
        }

        private static bool CreateEncrypted(ClParamEnum<MausEncryptionFormat> cEncFmt, out MausEncryptionFormat encFormat, out string ssPassword)
        {
            encFormat = MausEncryptionFormat.None;
            if (cEncFmt.IsSet) //Only true if Interactive is also true
            {
                encFormat = cEncFmt.Value.Value;

                if (EncryptionPrompt(null, encFormat, out ssPassword))
                    return true;
                return false;
            }
            ssPassword = null;
            return false;
        }

        private static void ShowHelp(ClParam[] clParams, bool showFull)
        {
            Console.Write('\t');
            Console.WriteLine(TextResources.Usage);

            StringBuilder commandName = new StringBuilder();
            if (Type.GetType("Mono.Runtime") != null)
                commandName.Append("mono ");
            commandName.Append(Path.GetFileName(typeof(Program).Assembly.Location));

            Console.WriteLine(TextResources.HelpCompress);
            Console.WriteLine(" > {0} -cf [{1}.maus] {2}1 {2}2 {2}3 ...", commandName, TextResources.HelpArchive, TextResources.HelpInput);
            Console.WriteLine();
            Console.WriteLine(TextResources.HelpDecompress);
            Console.WriteLine(" > {0} -xf [{1}.maus]", commandName, TextResources.HelpArchive, TextResources.HelpOutput);
            Console.WriteLine();
            Console.WriteLine(TextResources.HelpList);
            Console.WriteLine(" > {0} -lvf [{1}.maus]", commandName, TextResources.HelpArchive);
            Console.WriteLine();
            Console.WriteLine(TextResources.HelpHelp);
            Console.WriteLine(" > {0} --help", commandName);
            Console.WriteLine();

            if (showFull)
            {
                Console.Write('\t');
                Console.WriteLine(TextResources.Parameters);

                for (int i = 0; i < clParams.Length; i++)
                {
                    var curParam = clParams[i];

                    IEnumerable<string> paramList;

                    ClParamValueBase cParamValue = curParam as ClParamValueBase;

                    if (curParam.LongNames.Length == 0)
                        paramList = new string[0];
                    else if (cParamValue != null)
                    {
                        paramList = new string[] { string.Concat(curParam.LongNames[0], "=<", cParamValue.ArgName, ">") }.
                            Concat(new ArraySegment<string>(curParam.LongNames, 1, curParam.LongNames.Length - 1));
                    }
                    else paramList = curParam.LongNames;

                    paramList = paramList.Select((n, index) => "--" + n);
                    if (curParam.ShortName != '\0')
                    {
                        string shortName = "-" + curParam.ShortName;

                        if (cParamValue != null)
                            shortName += " <" + cParamValue.ArgName + ">";

                        paramList = new string[] { shortName }.Concat(paramList);
                    }

                    Console.WriteLine(string.Join(", ", paramList));

                    const string indent = "   ";
                    string[] helpMessages = curParam.HelpMessage.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < helpMessages.Length; j++)
                    {
                        Console.Write(indent);
                        Console.WriteLine(helpMessages[j]);
                    }
                    if (curParam == curParam.Parser.RawParam)
                    {
                        Console.Write(indent);
                        Console.WriteLine(TextResources.ParamRaw, curParam.ShortName == '\0' ? "--" + curParam.LongNames[0] : "-" + curParam.ShortName);
                    }
                    Console.WriteLine();
                }
            }
        }

#if DEBUG
        private static void GoThrow(Exception e)
        {
            Console.Error.WriteLine(e.GetType().ToString() + ":");
            Console.Error.WriteLine(e.ToString());
            Console.Error.WriteLine("Throw? Y/N> ");
            var key = Console.ReadKey().Key;
            Console.WriteLine();
            if (key == ConsoleKey.Y)
                throw new Exception(e.Message, e);
        }
#endif
        private static Regex GetRegex(string s)
        {
            return new Regex("^" + Regex.Escape(s).Replace("\\*", ".*").Replace("\\?", "."));
        }

        private static bool MatchesRegexAny(IEnumerable<Regex> regex, string path)
        {
            if (path == null || regex == null)
                return true;
            foreach (Regex curRegex in regex)
            {
                if (curRegex.IsMatch(path))
                    return true;
            }
            return false;
        }

        private static bool EncryptionPrompt(IMausCrypt ds, MausEncryptionFormat encFormat, out string ss)
        {
            bool notFound1 = true;
            ss = null;
            do
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("1. " + TextResources.EncryptedPrompt1Pwd);
                Console.WriteLine("2. " + TextResources.Cancel);
                Console.Write("> ");

                string line = Console.ReadLine().Trim(' ', '.');
                if (line.Length != 1) continue;

                switch (line[0])
                {
                    case '1':
                        Console.Write(TextResources.EncryptedPrompt1Pwd + ":");
                        List<char> charList = new List<char>();
                        ConsoleKeyInfo cKey;
                        do
                        {
                            cKey = Console.ReadKey();

                            if (cKey.Key == ConsoleKey.Backspace)
                            {
                                Console.Write(":");
                                if (charList.Count > 0)
                                    charList.RemoveAt(ss.Length - 1);
                            }
                            else if (cKey.Key != ConsoleKey.Enter)
                            {
                                Console.Write("\b \b");
                                charList.Add(cKey.KeyChar);
                            }
                        }
                        while (cKey.Key != ConsoleKey.Enter);
                        Console.WriteLine();
                        ss = new string(charList.ToArray());

                        if (ss.Length == 0)
                        {
                            Console.WriteLine(TextResources.PasswordZeroLength);
                            continue;
                        }

                        if (ds == null)
                            Console.WriteLine(TextResources.KeepSecret);
                        else
                            ds.Password = ss;
                        break;
                    case '2':
                        return true;
                    default:
                        continue;
                }
                if (ds == null)
                    notFound1 = false;
                else
                {
                    try
                    {
                        ds.Decrypt();
                        return false;
                    }
                    catch (CryptoException)
                    {
                        Console.Error.WriteLine(TextResources.EncryptedBadKey);
                        continue;
                    }
                }
            }
            while (notFound1);
            return false;
        }

        private static string NoOutputCreate(ClParam param)
        {
            return string.Format(TextResources.NoOutputCreate, param.Key);
        }

        private static string NoEntryExtract(ClParam param)
        {
            return string.Format(TextResources.NoEntryExtract, param.Key);
        }

        private static bool OverwritePrompt(ClParamFlag interactive, ClParamFlag overwrite, ClParamFlag skipexist, ClParamFlag verbose, ref string filename)
        {
            if (!File.Exists(filename))
                return false;

            if (verbose.IsSet || skipexist.IsSet || interactive.IsSet)
            {
                if (!skipexist.IsSet || interactive.IsSet)
                    Console.WriteLine(TextResources.OverwriteAlert, filename);
                else
                    Console.Error.WriteLine(TextResources.OverwriteAlert, filename);
            }

            if (skipexist.IsSet || (!overwrite.IsSet && interactive.IsSet && OverwritePrompt(ref filename)))
            {
                Console.WriteLine(TextResources.OverwriteSkip);
                return true;
            }

            return false;
        }

        private static int Return(int value, ClParamFlag interactive)
        {
            if (interactive.IsSet)
            {
                Console.WriteLine(TextResources.AnyKey);
                Console.ReadKey();
            }
            return value;
        }

        private static bool OverwritePrompt(ref string filename)
        {
            bool notFound = true;
            do
            {
                Console.Write(TextResources.OverwritePrompt + "> ");

                string line = Console.ReadLine().Trim();
                const string oYes = "yes", oNo = "no", oRen = "rename";

                if (string.IsNullOrEmpty(line))
                    continue;

                if (oYes.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(oYes, StringComparison.OrdinalIgnoreCase) ||
                    TextResources.OverYes.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(TextResources.OverNo, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(TextResources.Overwrite);
                    notFound = false;
                }
                else if (oNo.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(oNo, StringComparison.OrdinalIgnoreCase) ||
                    TextResources.OverNo.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(TextResources.OverNo, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (oRen.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(oRen, StringComparison.OrdinalIgnoreCase) ||
                    TextResources.OverRename.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(TextResources.OverRename, StringComparison.OrdinalIgnoreCase))
                {
                    string ext = Path.GetExtension(filename);
                    string newPath = string.IsNullOrEmpty(ext) ? filename + ".out" : Path.ChangeExtension(filename, ".out" + ext);
                    if (!File.Exists(newPath))
                    {
                        filename = newPath;
                        Console.WriteLine(TextResources.OverwriteRename, newPath);
                        return false;
                    }

                    checked
                    {
                        for (ulong i = 1; i != 0; i++)
                        {
                            string longText = i.ToString(NumberFormatInfo.InvariantInfo);

                            string longPath = string.IsNullOrEmpty(ext) ? string.Concat(filename, ".out", longText) :
                                Path.ChangeExtension(filename, string.Concat(filename, ".out", longText, ext));

                            if (!File.Exists(longPath))
                            {
                                Console.WriteLine(TextResources.OverwriteRename, longPath);
                                filename = longPath;
                                return false;
                            }
                        }
                    }
                }
            }
            while (notFound);
            return false;
        }

        internal const int BufferSize = 8192;

        private static AsymmetricKeyParameter LoadKeyFile(string path, BigInteger index, bool getPrivate, ClParamFlag interactive)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine(TextResources.FileNotFound, path);
                return null;
            }

            try
            {
                using (FileStream ms = File.OpenRead(path))
                {
                    object keyObj = null;
                    #region PuTTY .ppk
                    if (keyObj == null)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        PuttyPpkReader reader = new PuttyPpkReader(ms, new ClPassword(interactive));

                        try
                        {
                            if (reader.Init())
                            {
                                if (getPrivate)
                                {
                                    if (!reader.EncType.Equals("none", StringComparison.Ordinal))
                                        Console.WriteLine(reader.Comment);

                                    do
                                    {
                                        try
                                        {
                                            keyObj = reader.ReadKeyPair().Private;
                                        }
                                        catch (InvalidCipherTextException)
                                        {
                                            Console.WriteLine(TextResources.BadPassword);
                                        }
                                    }
                                    while (keyObj == null);
                                }
                                else keyObj = reader.ReadPublicKey();

                                if (index != null && index.CompareTo(BigInteger.Zero) != 0)
                                {
                                    Console.Error.WriteLine(TextResources.IndexFile, index);
                                    return null;
                                }
                            }
                        }
                        catch (InvalidDataException)
                        {
                            Console.Error.WriteLine(getPrivate ? TextResources.SignBadPrivate : TextResources.SignBadPublic);
                        }
                    }
                    #endregion

                    #region PEM
                    do
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        using (StreamReader sr = new StreamReader(ms, Encoding.UTF8, true, BufferSize, true))
                        {
                            PemReader reader = new PemReader(sr, new ClPassword(interactive));
                            try
                            {
                                keyObj = reader.ReadObject();

                                if (keyObj == null) break;
                                else if (index != null && index.CompareTo(BigInteger.Zero) != 0)
                                {
                                    Console.Error.WriteLine(TextResources.IndexFile, index);
                                    return null;
                                }
                            }
                            catch (InvalidCipherTextException)
                            {
                                Console.Error.WriteLine(TextResources.BadPassword);
                                continue;
                            }
                            catch (IOException x)
                            {
                                if (x.Message == "base64 data appears to be truncated")
                                {
                                    keyObj = null;
                                    break;
                                }
                                else
                                {
                                    Console.Error.WriteLine(x.Message);
#if DEBUG
                                    GoThrow(x);
#endif
                                    return null;
                                }
                            }
                        }
                    }
                    while (keyObj == null);
                    #endregion

                    #region PGP - Private Key
                    if (keyObj == null)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        if (index == null) index = BigInteger.Zero;

                        try
                        {
                            var decoderStream = PgpUtilities.GetDecoderStream(ms);
                            PgpSecretKeyRing bundle = new PgpSecretKeyRing(decoderStream);

                            BigInteger counter = BigInteger.ValueOf(-1);

                            foreach (PgpSecretKey pKey in bundle.GetSecretKeys())
                            {
                                counter = counter.Add(BigInteger.One);

                                if (counter.CompareTo(index) < 0)
                                    continue;

                                if (!getPrivate)
                                {
                                    keyObj = pKey.PublicKey.GetKey();
                                    break;
                                }

                                if (pKey.KeyEncryptionAlgorithm == 0)
                                {
                                    PgpPrivateKey privKey = pKey.ExtractPrivateKeyUtf8(null);
                                    keyObj = privKey.Key;
                                }

                                do
                                {
                                    char[] password = null;
                                    try
                                    {
                                        password = new ClPassword(interactive).GetPassword();
                                        PgpPrivateKey privKey = pKey.ExtractPrivateKeyUtf8(password);

                                        keyObj = privKey.Key;
                                    }
                                    catch (PgpException)
                                    {
                                        if (password != null && password.Length == 0)
                                            return null;
                                        Console.Error.WriteLine(TextResources.BadPassword);
                                    }
                                }
                                while (keyObj == null); break;
                            }

                            if (keyObj == null && counter.CompareTo(index) < 0)
                            {
                                Console.Error.WriteLine(TextResources.IndexFile, index);
                                return null;
                            }
                        }
                        catch (IOException x)
                        {
                            if (!x.Message.StartsWith("secret key ring doesn't start with", StringComparison.OrdinalIgnoreCase))
#if DEBUG
                                GoThrow(x);
#else
                                throw;
#endif
                        }
                    }
                    #endregion

                    #region PGP - Public Key
                    if (keyObj == null)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        try
                        {
                            var decoderStream = PgpUtilities.GetDecoderStream(ms);
                            PgpPublicKeyRing bundle = new PgpPublicKeyRing(decoderStream);

                            BigInteger counter = BigInteger.ValueOf(-1);

                            foreach (PgpPublicKey pKey in bundle.GetPublicKeys())
                            {
                                counter = counter.Add(BigInteger.One);

                                if (counter.CompareTo(index) < 0)
                                    continue;

                                if (getPrivate)
                                {
                                    Console.Error.WriteLine(TextResources.SignNeedPrivate);
                                    return null;
                                }

                                keyObj = pKey.GetKey();
                                break;
                            }

                            if (keyObj == null && counter.CompareTo(index) < 0)
                            {
                                Console.Error.WriteLine(TextResources.IndexFile, index);
                                return null;
                            }
                        }
                        catch (IOException x)
                        {
                            if (!x.Message.StartsWith("public key ring doesn't start with", StringComparison.OrdinalIgnoreCase))
#if DEBUG
                                GoThrow(x);
#else
                                throw;
#endif
                        }
                    }
                    #endregion

                    #region OpenSSL .pub/authorized_keys
                    if (keyObj == null)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        if (index == null) index = BigInteger.Zero;

                        using (StreamReader sr = new StreamReader(ms, Encoding.UTF8, true, BufferSize, true))
                        {
                            BigInteger counter = BigInteger.ValueOf(-1);
                            foreach (Tuple<string, AsymmetricKeyParameter, string> curVal in AuthorizedKeysParser(sr))
                            {
                                counter = counter.Add(BigInteger.One);

                                if (counter.CompareTo(index) < 0)
                                    continue;

                                if (getPrivate)
                                {
                                    Console.Error.WriteLine(TextResources.SignNeedPrivate);
                                    return null;
                                }

                                if (curVal.Item2 == null)
                                {
                                    Console.Error.WriteLine(TextResources.UnknownKeyType, curVal.Item1);
                                    return null;
                                }

                                return curVal.Item2;
                            }
                            if (counter.CompareTo(index) < 0)
                            {
                                Console.Error.WriteLine(TextResources.IndexFile, index);
                                return null;
                            }
                        }
                    }
                    #endregion

                    AsymmetricCipherKeyPair pair = keyObj as AsymmetricCipherKeyPair;
                    if (pair != null)
                    {
                        if (getPrivate)
                        {
                            if (pair.Private is RsaKeyParameters || pair.Private is DsaKeyParameters || pair.Private is ECKeyParameters)
                                return pair.Private;

                            Console.Error.WriteLine(TextResources.UnknownKeyType);
                            return null;
                        }

                        if (pair.Public is RsaKeyParameters || pair.Public is DsaKeyParameters || pair.Public is ECKeyParameters)
                            return pair.Public;

                        Console.Error.WriteLine(TextResources.UnknownKeyType);
                        return null;
                    }

                    if (!(keyObj is AsymmetricKeyParameter))
                    {
                        Console.Error.WriteLine(TextResources.UnknownKeyType);
                        return null;
                    }

                    RsaKeyParameters singleRsa = keyObj as RsaKeyParameters;
                    if (singleRsa != null)
                    {
                        if (singleRsa is RsaPrivateCrtKeyParameters)
                            return singleRsa;

                        if (getPrivate)
                        {
                            Console.Error.WriteLine(singleRsa.IsPrivate ? TextResources.SignBadPrivate : TextResources.SignNeedPrivate);
                            return null;
                        }

                        if (singleRsa.IsPrivate)
                        {
                            Console.Error.WriteLine(TextResources.SignBadPublic);
                            return null;
                        }

                        return singleRsa;
                    }

                    DsaKeyParameters singleDsa = keyObj as DsaKeyParameters;
                    if (singleDsa != null)
                    {
                        if (singleDsa is DsaPrivateKeyParameters)
                            return singleDsa;

                        if (getPrivate)
                        {
                            Console.Error.WriteLine(singleDsa.IsPrivate ? TextResources.SignBadPrivate : TextResources.SignNeedPrivate);
                            return null;
                        }

                        if (singleDsa is DsaPublicKeyParameters)
                            return singleDsa;
                        Console.Error.WriteLine(TextResources.SignBadPublic);
                        return null;
                    }

                    ECKeyParameters singleEcdsa = keyObj as ECKeyParameters;
                    if (singleEcdsa == null)
                    {
                        Console.Error.WriteLine(TextResources.UnknownKeyType);
                        return null;
                    }

                    if (singleEcdsa is ECPrivateKeyParameters)
                        return singleEcdsa;

                    if (getPrivate)
                    {
                        Console.Error.WriteLine(singleEcdsa.IsPrivate ? TextResources.SignBadPrivate : TextResources.SignNeedPrivate);
                        return null;
                    }

                    if (singleEcdsa is ECPublicKeyParameters)
                        return singleEcdsa;

                    Console.Error.WriteLine(TextResources.SignBadPublic);
                    return null;
                }
            }
            catch (Exception x)
            {
                Console.Error.WriteLine(x.Message);
#if DEBUG
                GoThrow(x);
#endif
                return null;
            }
        }

        private static IEnumerable<Tuple<string, AsymmetricKeyParameter, string>> AuthorizedKeysParser(TextReader reader)
        {
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.Length == 0 || line[0] == '#')
                    continue;

                string[] words = line.Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);

                if (words.Length < 2) throw new InvalidDataException(TextResources.SignBadPublic);
                byte[] buffer;
                try
                {
                    buffer = Convert.FromBase64String(words[1]);
                }
                catch (FormatException)
                {
                    throw new InvalidDataException(TextResources.SignBadPublic);
                }

                int curPos = 0;
                string type = words[0], comment = words.Length == 2 ? string.Empty : words[2];

                if (type.Length > 64 || type != ReadString(buffer, ref curPos))
                    throw new InvalidDataException(TextResources.SignBadPublic);

                if (type.Equals("ssh-rsa", StringComparison.Ordinal))
                {
                    yield return new Tuple<string, AsymmetricKeyParameter, string>(type, ReadRSAParams(buffer, ref curPos), comment);
                    continue;
                }
                if (type.Equals("ssh-dss", StringComparison.Ordinal))
                {
                    yield return new Tuple<string, AsymmetricKeyParameter, string>(type, ReadDSAParams(buffer, ref curPos), comment);
                    continue;
                }
                if (type.StartsWith("ecdsa-sha2-", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Tuple<string, AsymmetricKeyParameter, string>(type, ReadECParams(type, buffer, ref curPos), comment);
                    continue;
                }

                yield return new Tuple<string, AsymmetricKeyParameter, string>(type, null, comment);
            }
        }

        internal static RsaKeyParameters ReadRSAParams(byte[] buffer, ref int curPos)
        {
            BigInteger exp = ReadBigInteger(buffer, ref curPos);
            BigInteger mod = ReadBigInteger(buffer, ref curPos);
            if (curPos != buffer.Length) throw new InvalidDataException(TextResources.SignBadPublic);
            return new RsaKeyParameters(false, mod, exp);
        }

        internal static DsaPublicKeyParameters ReadDSAParams(byte[] buffer, ref int curPos)
        {
            BigInteger p = ReadBigInteger(buffer, ref curPos);
            BigInteger q = ReadBigInteger(buffer, ref curPos);
            BigInteger g = ReadBigInteger(buffer, ref curPos);
            BigInteger y = ReadBigInteger(buffer, ref curPos);
            if (curPos != buffer.Length) throw new InvalidDataException(TextResources.SignBadPublic);
            return new DsaPublicKeyParameters(y, new DsaParameters(p, g, g));
        }

        internal static ECPublicKeyParameters ReadECParams(string type, byte[] buffer, ref int curPos)
        {
            string s = ReadString(buffer, ref curPos);
            byte[] qBytes = ReadBuffer(buffer, ref curPos);

            const int typePrefixLen = 11;
            if (curPos != buffer.Length || !type.Substring(typePrefixLen).Equals(s, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(TextResources.SignBadPublic);

            X9ECParameters ecSpec;
            DerObjectIdentifier dId;

            if (s.StartsWith("nistp", StringComparison.OrdinalIgnoreCase))
            {
                dId = NistNamedCurves.GetOid("P-" + s.Substring(5));
                ecSpec = NistNamedCurves.GetByOid(dId);
                if (ecSpec == null)
                    throw new InvalidDataException(TextResources.SignBadPublic);
            }
            else
            {
                try
                {
                    dId = new DerObjectIdentifier(s);
                }
                catch
                {
                    throw new InvalidDataException(TextResources.SignBadPublic);
                }

                ecSpec = ECNamedCurveTable.GetByOid(dId);

                if (ecSpec == null)
                {
                    ecSpec = CustomNamedCurves.GetByOid(dId);

                    if (ecSpec == null)
                        throw new InvalidDataException(TextResources.UnknownCurve);
                }
            }

            try
            {
                return new ECPublicKeyParameters("ECDSA", new X9ECPoint(ecSpec.Curve, qBytes).Point, dId);
            }
            catch
            {
                throw new InvalidDataException(TextResources.SignBadPublic);
            }
        }

        internal static int ReadInt(byte[] buffer, ref int curPos)
        {
            if (curPos + sizeof(int) > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);
            return (buffer[curPos++] << 24) | (buffer[curPos++] << 16) | (buffer[curPos++] << 8) | buffer[curPos++];
        }

        internal static string ReadString(byte[] buffer, ref int curPos)
        {
            int len = ReadInt(buffer, ref curPos);
            if (len <= 0 || curPos + len > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);
            string returner = Encoding.UTF8.GetString(buffer, curPos, len);
            curPos += len;
            return returner;
        }

        internal static BigInteger ReadBigInteger(byte[] buffer, ref int curPos)
        {
            int len = ReadInt(buffer, ref curPos);
            if (len <= 0 || curPos + len > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);
            BigInteger returner = new BigInteger(buffer, curPos, len);
            curPos += len;
            return returner;
        }

        internal static byte[] ReadBuffer(byte[] buffer, ref int curPos)
        {
            int len = ReadInt(buffer, ref curPos);
            if (len <= 0 || curPos + len > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);

            byte[] returner = new byte[len];
            Array.Copy(buffer, curPos, returner, 0, len);
            curPos += len;
            return returner;
        }
    }

    internal class ClPassword : IPasswordFinder
    {
        private ClParam _interactive;

        public ClPassword(ClParam interactive)
        {
            _interactive = interactive;
        }

        public char[] GetPassword()
        {
            if (!_interactive.IsSet)
                throw new InvalidDataException(TextResources.NoPassword);

            List<char> charList = new List<char>();
            Console.WriteLine(TextResources.NoPassword);
            Console.Write("> ");

            ConsoleKeyInfo c;

            while ((c = Console.ReadKey()).Key != ConsoleKey.Enter)
            {
                if (c.Key == ConsoleKey.Backspace)
                {
                    charList.RemoveAt(charList.Count - 1);
                    Console.Write(" ");
                }
                else
                {
                    Console.Write("\b \b");
                    charList.Add(c.KeyChar);
                }
            }
            Console.WriteLine(">");

            return charList.ToArray();
        }
    }
}
