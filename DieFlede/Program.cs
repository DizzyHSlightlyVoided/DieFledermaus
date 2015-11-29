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
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using DieFledermaus.Cli.Globalization;

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

            ClParamFlag help = new ClParamFlag(TextResources.HelpMHelp, 'h', "help", TextResources.PNameHelp);
            ClParamFlag create = new ClParamFlag(TextResources.HelpMCreate, 'c', "create", TextResources.PNameCreate);
            ClParamFlag extract = new ClParamFlag(TextResources.HelpMExtract, 'x', "extract", TextResources.PNameExtract);
            extract.MutualExclusives.Add(create);
            create.MutualExclusives.Add(extract);

            ClParamFlag interactive = new ClParamFlag(TextResources.HelpMInteractive, 'i', "interactive", TextResources.PNameInteractive);

            ClParamFlag overwrite = new ClParamFlag(TextResources.HelpMOverwrite, 'w', "overWrite", TextResources.PNameOverwrite);
            ClParamFlag skipexist = new ClParamFlag(TextResources.HelpMSkip, 's', "skip", "skip-existing",
                TextResources.PNameSkip, TextResources.PNameSkipExisting);
            skipexist.MutualExclusives.Add(overwrite);

            ClParamFlag verbose = new ClParamFlag(TextResources.HelpMVerbose, 'v', "verbose", TextResources.PNameVerbose);

            ClParamValue archiveFile = new ClParamValue(TextResources.HelpMArchive, TextResources.HelpArchive, 'f', "file", "archive",
                TextResources.PNameFile, TextResources.PNameArchive);
            archiveFile.ConvertValue = Path.GetFullPath;

            ClParamValue entryFile = new ClParamValue(TextResources.HelpMEntry, TextResources.HelpInput, 'e', "entry", "input",
                TextResources.PNameEntry, TextResources.PNameInput);
            entryFile.ConvertValue = Path.GetFullPath;
            entryFile.MutualExclusives.Add(extract);
            extract.OtherMessages.Add(entryFile, NoEntryExtract);

            ClParamValue outFile = new ClParamValue(TextResources.HelpMOut, TextResources.HelpOutput, 'o', "out", "output",
                TextResources.PNameOut, TextResources.PNameOutput);
            outFile.ConvertValue = Path.GetFullPath;
            outFile.MutualExclusives.Add(create);
            create.OtherMessages.Add(outFile, NoOutputCreate);

            outFile.MutualExclusives.Add(entryFile);
            entryFile.MutualExclusives.Add(outFile);

            extract.SetAction = delegate (ClParser p)
            {
                p.RawParam = outFile;
                if (entryFile.IsSet && string.IsNullOrEmpty(entryFile.Key))
                {
                    outFile.Key = string.Empty;
                    if (outFile.SetValue(entryFile.Value))
                        return true;
                    outFile.IsSet = true;
                    entryFile.Value = null;
                    entryFile.Key = null;
                    entryFile.IsSet = false;
                }
                return false;
            };

            ClParamFlag encAes = new ClParamFlag(TextResources.HelpMAes, '\0', "AES");
            encAes.MutualExclusives.Add(extract);
            extract.OtherMessages.Add(encAes, NoEntryExtract);

            ClParamValue encKeyFile = new ClParamValue(TextResources.HelpMKeyFile, TextResources.HelpInput, '\0', "keyfile", TextResources.PNameKeyFile);

            ClParamValue encSaveKey = new ClParamValue(TextResources.HelpMSaveKey, TextResources.HelpOutput, '\0', "savekey", TextResources.PNameSaveKey);
            encSaveKey.MutualExclusives.Add(extract);
            encSaveKey.MutualExclusives.Add(encKeyFile);
            extract.OtherMessages.Add(encSaveKey, NoEntryExtract);

            ClParam[] clParams = { create, extract, help, entryFile, archiveFile, outFile, interactive, verbose, skipexist, overwrite,
                encAes, encKeyFile, encSaveKey };

            if (args.Length == 1 && args[0][0] != '-')
            {
                archiveFile.IsSet = true;
                archiveFile.Value = args[0];
                extract.IsSet = true;
            }
            else
            {
                using (ClParser parser = new ClParser(Array.IndexOf(clParams, entryFile), clParams))
                {
                    if (parser.Parse(args))
                    {
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }
                }
            }
            bool acting = false;

            bool hasEncryptionOptions = (encKeyFile.IsSet || encSaveKey.IsSet);

            if (args.Length > 0)
            {
                if (extract.IsSet)
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
                    else acting = true;
                }
                else if (create.IsSet)
                {
                    if (!entryFile.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.CreateNoEntry);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }
                    else if (!encAes.IsSet && hasEncryptionOptions)
                    {
                        Console.Error.WriteLine(TextResources.EncryptionNoEncryption);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }
                    else if (encAes.IsSet && !interactive.IsSet && !hasEncryptionOptions)
                    {
                        Console.Error.WriteLine(TextResources.EncryptionNoOpts);
                        Console.Error.WriteLine(TextResources.EncryptionNoOpts2);
                        ShowHelp(clParams, false);
                        return Return(-1, interactive);
                    }
                    else acting = true;
                }
                else if (archiveFile.IsSet || entryFile.IsSet || outFile.IsSet)
                {
                    Console.Error.WriteLine(TextResources.RequireAtLeastOne, "-c, -x");
                    ShowHelp(clParams, false);
                    return Return(-1, interactive);
                }
                else if (!help.IsSet)
                {
                    Console.Error.WriteLine(TextResources.RequireAtLeastOne, "-c, -x, --help");
                    ShowHelp(clParams, false);
                    return Return(-1, interactive);
                }
            }

            byte[] key = null;

            if (encKeyFile.IsSet)
            {
                if (!File.Exists(encKeyFile.Value))
                {
                    Console.Error.WriteLine(TextResources.FileNotFound, encKeyFile.Value);
                    return Return(-1, interactive);
                }
                else
                {
                    try
                    {
                        key = File.ReadAllBytes(encKeyFile.Value);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.Message);
#if DEBUG
                        GoThrow(e);
#endif
                        return e.HResult;
                    }
                }
            }

            if (key != null)
            {
                if (!CheckKeyLength(null, MausEncryptionFormat.Aes, key))
                {
                    Console.Error.WriteLine(encKeyFile.IsSet ? TextResources.EncryptInvalidFileLength : TextResources.EncryptInvalidKeyLength);
                    Console.Error.WriteLine(PromptKeyLength(null, MausEncryptionFormat.Aes));
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

            SecureString ssPassword = null;

            const string mausExt = ".maus";
            const int extLen = 5;
            try
            {
                #region Extract
                if (extract.IsSet)
                {
                    DateTime? cTime, mTime;
                    using (FileStream fs = File.OpenRead(archiveFile.Value))
                    using (DieFledermausStream ds = new DieFledermausStream(fs, CompressionMode.Decompress))
                    {
                        if (ds.EncryptionFormat != MausEncryptionFormat.None)
                        {
                            if (!hasEncryptionOptions)
                            {
                                if (interactive.IsSet)
                                {
                                    Console.WriteLine(TextResources.EncryptionNoOptsExtract);
                                    if (EncryptionPrompt(ds, ds.EncryptionFormat, null, out key, out ssPassword))
                                        return -4;
                                    if (ssPassword != null)
                                        ds.SetPassword(ssPassword);
                                }
                                else
                                {
                                    Console.Error.WriteLine(TextResources.EncryptionNoOptsExtract);
                                    Console.Error.WriteLine(TextResources.EncryptionNoOpts2);
                                    return -4;
                                }
                            }
                            if (key != null)
                            {
                                if (!ds.IsValidKeyByteSize(key.Length))
                                {
                                    Console.Error.WriteLine(encKeyFile.IsSet ? TextResources.EncryptInvalidFileLength :
                                        TextResources.EncryptInvalidKeyLength);
                                }

                                ds.Key = key;
                            }

                            try
                            {
                                ds.LoadData();
                            }
                            catch (CryptographicException)
                            {
                                Console.Error.WriteLine(TextResources.EncryptedBadKey);
                                return Return(-4, interactive);
                            }
                        }
                        else if (hasEncryptionOptions && verbose.IsSet)
                            Console.WriteLine(TextResources.EncryptionNot);

                        if (outFile.Value == null)
                        {
                            if (ds.Filename != null)
                            {
                                outFile.Value = Path.GetFullPath(ds.Filename);
                            }
                            else if (archiveFile.Value.Length > extLen && mausExt.Equals(Path.GetExtension(archiveFile.Value), StringComparison.OrdinalIgnoreCase))
                            {
                                outFile.Value = archiveFile.Value.Substring(0, archiveFile.Value.Length - extLen);
                            }
                            else
                            {
                                outFile.Value = archiveFile.Value + ".out";
                                Console.WriteLine(TextResources.RenameExtract, outFile.Value);
                            }
                        }

                        if (File.Exists(outFile.Value))
                        {
                            if (verbose.IsSet || skipexist.IsSet || interactive.IsSet)
                            {
                                string message;
                                if (outFile.Value.Equals(archiveFile.Value, StringComparison.Ordinal))
                                {
                                    Console.Error.WriteLine(TextResources.OverwriteSameArchive, archiveFile.Value);
                                    return Return(-3, interactive);
                                }
                                else message = string.Format(TextResources.OverwriteAlert, outFile.Value);

                                if (!skipexist.IsSet)
                                    Console.WriteLine(message);
                                else
                                    Console.Error.WriteLine(message);
                            }

                            if (OverwritePrompt(interactive, overwrite, skipexist, ref outFile.Value))
                                return -3;
                        }

                        using (FileStream outStream = File.Create(outFile.Value))
                            ds.CopyTo(outStream);

                        if (verbose.IsSet)
                            Console.WriteLine(TextResources.Completed);

                        cTime = ds.CreatedTime;
                        mTime = ds.ModifiedTime;
                    }

                    FileInfo fInfo = new FileInfo(outFile.Value);

                    if (cTime.HasValue)
                    {
                        try
                        {
                            fInfo.CreationTimeUtc = cTime.Value;
                        }
                        catch
                        {
                            if (verbose.IsSet)
                                Console.Error.WriteLine(TextResources.TimeCSet);
                        }
                    }
                    if (mTime.HasValue)
                    {
                        try
                        {
                            fInfo.LastWriteTimeUtc = mTime.Value;
                        }
                        catch
                        {
                            if (verbose.IsSet)
                                Console.Error.WriteLine(TextResources.TimeMSet);
                        }
                    }

                    return Return(0, interactive);
                }
                #endregion

                #region Create
                FileInfo entryInfo = new FileInfo(entryFile.Value);
                using (FileStream fs = File.OpenRead(entryFile.Value))
                {
                    MausEncryptionFormat encFormat = MausEncryptionFormat.None;
                    MausCompressionFormat compFormat = MausCompressionFormat.Deflate;

                    if (encAes.IsSet)
                    {
                        encFormat = MausEncryptionFormat.Aes;

                        if (interactive.IsSet && !hasEncryptionOptions)
                        {
                            Console.WriteLine(TextResources.EncryptionNoOpts);

                            if (EncryptionPrompt(null, encFormat, encSaveKey, out key, out ssPassword))
                                return -4;
                        }
                    }

                    if (archiveFile.Value == null)
                        archiveFile.Value = entryFile.Value + mausExt;

                    if (archiveFile.Value.Equals(entryFile.Value, StringComparison.Ordinal))
                    {
                        Console.WriteLine(TextResources.OverwriteSameEntry, entryFile.Value);
                        return Return(-3, interactive);
                    }

                    if (File.Exists(archiveFile.Value))
                    {
                        if (verbose.IsSet || skipexist.IsSet || interactive.IsSet)
                        {
                            if (!skipexist.IsSet || interactive.IsSet)
                                Console.WriteLine(TextResources.OverwriteAlert, archiveFile.Value);
                            else
                                Console.Error.WriteLine(TextResources.OverwriteAlert, archiveFile.Value);
                        }

                        if (OverwritePrompt(interactive, overwrite, skipexist, ref archiveFile.Value))
                            return -3;
                    }

                    if (encAes.IsSet && key == null && ssPassword == null)
                    {
                        key = new byte[GetKeyLength(encFormat) >> 3];
                        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                            rng.GetBytes(key);
                    }

                    if (encSaveKey.IsSet)
                    {
                        if (encSaveKey.Value.Equals(archiveFile.Value))
                        {
                            Console.Error.WriteLine(TextResources.OverwriteEncryptSameArchive, encSaveKey.Value);
                            return Return(-3, interactive);
                        }

                        if (encSaveKey.Value.Equals(entryFile.Value))
                        {
                            Console.Error.WriteLine(TextResources.OverwriteEncryptSameEntry, encSaveKey.Value);
                            return Return(-3, interactive);
                        }

                        if (File.Exists(encSaveKey.Value))
                        {
                            if (interactive.IsSet)
                                Console.WriteLine(TextResources.OverwriteAlert, encSaveKey.Value);
                            else
                            {
                                Console.Error.WriteLine(TextResources.OverwriteAlert, encSaveKey.Value);
                                return -3;
                            }

                            if (OverwritePrompt(interactive, overwrite, skipexist, ref encSaveKey.Value))
                                return -3;
                        }

                        File.WriteAllBytes(encSaveKey.Value, key);
                        if (verbose.IsSet)
                        {
                            Console.WriteLine(TextResources.EncryptionSaved, encSaveKey.Value);
                            Console.WriteLine(TextResources.KeepSecret);
                        }
                    }

                    using (Stream arStream = File.Create(archiveFile.Value))
                    using (DieFledermausStream ds = new DieFledermausStream(arStream, compFormat, encFormat))
                    {
                        if (key != null)
                            ds.Key = key;
                        else if (ssPassword != null)
                            ds.SetPassword(ssPassword);

                        try
                        {
                            ds.CreatedTime = entryInfo.CreationTimeUtc;
                        }
                        catch
                        {
                            if (verbose.IsSet)
                                Console.WriteLine(TextResources.TimeCGet);
                        }

                        try
                        {
                            ds.ModifiedTime = entryInfo.LastWriteTimeUtc;
                        }
                        catch
                        {
                            if (verbose.IsSet)
                                Console.WriteLine(TextResources.TimeMGet);
                        }

                        ds.Filename = Path.GetFileName(entryFile.Value);
                        fs.CopyTo(ds);
                    }
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
                if (ssPassword != null)
                    ssPassword.Dispose();
            }
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
            Console.WriteLine(" > {0} -cf [{1}.maus] [{2}]", commandName, TextResources.HelpArchive, TextResources.HelpInput);
            Console.WriteLine();
            Console.WriteLine(TextResources.HelpDecompress);
            Console.WriteLine(" > {0} -xf [{1}.maus] [{2}]", commandName, TextResources.HelpArchive, TextResources.HelpOutput);
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

                    ClParamValue cParamValue = curParam as ClParamValue;
                    if (cParamValue != null)
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
                    Console.Write("  ");
                    Console.WriteLine(curParam.HelpMessage);
                    Console.WriteLine();
                }

            }
        }

        private static bool TryGetKeyHex(string s, out byte[] key)
        {
            string hexVal = new string(s.Where(i => !char.IsWhiteSpace(i)).ToArray());
            key = new byte[(int)Math.Ceiling(hexVal.Length / 2.0)];
            for (int i = 0; i < key.Length; i++)
            {
                byte curByte;
                if (byte.TryParse(hexVal.Substring(i << 1, Math.Min(2, hexVal.Length - i)), NumberStyles.HexNumber,
                    NumberFormatInfo.InvariantInfo, out curByte))
                {
                    key[i] = curByte;
                }
                else
                {
                    key = null;
                    Console.Error.WriteLine(TextResources.EncryptInvalidHex);
                    return false;
                }
            }
            return true;
        }

#if DEBUG
        private static void GoThrow(Exception e)
        {
            Console.Error.WriteLine("Throw? Y/N> ");
            if (Console.ReadKey().Key == ConsoleKey.Y)
                throw new Exception(e.Message, e);
        }
#endif

        private static bool EncryptionPrompt(DieFledermausStream ds, MausEncryptionFormat encFormat, ClParamValue encSaveKey, out byte[] key, out SecureString ss)
        {
            bool notFound1 = true;
            ss = null;
            do
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("1. " + TextResources.EncryptedPrompt1Pwd);
                Console.WriteLine("2. " + TextResources.EncryptedPrompt2KeyHex);
                Console.WriteLine("3. " + TextResources.EncryptedPrompt3KeyB64);
                Console.WriteLine("4. " + TextResources.EncryptedPrompt4KeyFile);
                if (ds == null)
                {
                    Console.WriteLine("5. " + TextResources.EncryptedPrompt5SaveKey);
                    Console.WriteLine("6. " + TextResources.Cancel);
                }
                else Console.WriteLine("5. " + TextResources.Cancel);
                Console.Write("> ");

                string line = Console.ReadLine().Trim(' ', '.');
                if (line.Length != 1) continue;

                switch (line[0])
                {
                    case '1':
                        Console.Write(TextResources.EncryptedPrompt1Pwd + ":");
                        key = null;
                        ss = new SecureString();
                        ConsoleKeyInfo cKey;
                        do
                        {
                            cKey = Console.ReadKey();

                            if (cKey.Key == ConsoleKey.Backspace)
                            {
                                Console.Write(":");
                                if (ss.Length > 0)
                                    ss.RemoveAt(ss.Length - 1);
                            }
                            else if (cKey.Key != ConsoleKey.Enter)
                            {
                                Console.Write("\b \b");
                                ss.AppendChar(cKey.KeyChar);
                            }
                        }
                        while (cKey.Key != ConsoleKey.Enter);
                        Console.WriteLine();
                        ss.MakeReadOnly();

                        if (ss.Length == 0)
                        {
                            Console.WriteLine(TextResources.PasswordZeroLength);
                            ss.Dispose();
                            continue;
                        }

                        if (ds != null)
                        {
                            ds.SetPassword(ss);
                            ss.Dispose();
                            ss = null;
                        }
                        break;
                    case '2':
                        Console.WriteLine(PromptKeyLength(ds, encFormat));
                        Console.Write(TextResources.EncryptedPrompt2KeyHex + "> ");

                        if (!TryGetKeyHex(Console.ReadLine(), out key))
                        {
                            Console.Error.WriteLine(TextResources.EncryptInvalidHex);
                            continue;
                        }

                        if (CheckKeyLength(ds, encFormat, key))
                            break;

                        continue;
                    case '3':
                        Console.WriteLine(PromptKeyLength(ds, encFormat));
                        Console.Write(TextResources.EncryptedPrompt3KeyB64 + "> ");
                        try
                        {
                            key = Convert.FromBase64String(Console.ReadLine());
                        }
                        catch (FormatException)
                        {
                            Console.Error.WriteLine(TextResources.EncryptInvalidBase64);
                            continue;
                        }

                        if (CheckKeyLength(ds, encFormat, key))
                            break;

                        continue;
                    case '4':
                        Console.WriteLine(PromptKeyLength(ds, encFormat));
                        Console.Write(TextResources.EncryptedPrompt4KeyFile + "> ");

                        line = Console.ReadLine().Trim();

                        if (!File.Exists(line))
                        {
                            Console.Error.WriteLine(TextResources.FileNotFound, line);
                            continue;
                        }

                        try
                        {
                            key = File.ReadAllBytes(line);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
#if DEBUG
                            GoThrow(e);
#endif
                            continue;
                        }

                        if (CheckKeyLength(ds, encFormat, key))
                            break;

                        continue;
                    case '5':
                        key = null;
                        if (ds != null)
                            return true;

                        Console.Write(TextResources.EncryptionSave + "> ");
                        encSaveKey.Value = Console.ReadLine();
                        encSaveKey.IsSet = true;

                        break;
                    case '6':
                        if (ds == null)
                        {
                            key = null;
                            return true;
                        }

                        continue;
                    default:
                        continue;
                }
                if (ds == null)
                    notFound1 = false;
                else
                {
                    try
                    {
                        ds.LoadData();
                        return false;
                    }
                    catch (CryptographicException)
                    {
                        Console.Error.WriteLine(TextResources.EncryptedBadKey);
                        continue;
                    }
                }
            }
            while (notFound1);
            key = null;
            return false;
        }

        private static int GetKeyLength(MausEncryptionFormat encFormat)
        {
            var keySizes = DieFledermausStream.GetKeySizes(encFormat);

            return keySizes.MaxSize;
        }

        private static string PromptKeyLength(DieFledermausStream ds, MausEncryptionFormat encFormat)
        {
            if (ds == null)
            {
                int keyMax = GetKeyLength(encFormat);

                return string.Format(TextResources.EncryptKeyLength, keyMax, keyMax >> 3);
            }
            else return string.Format(TextResources.EncryptKeyLength, ds.KeySizes.MaxSize, ds.KeySizes.MaxSize >> 3);
        }

        private static bool CheckKeyLength(DieFledermausStream ds, MausEncryptionFormat encFormat, byte[] key)
        {
            if (ds == null)
                return key.Length == GetKeyLength(encFormat) >> 3;

            if (!ds.IsValidKeyByteSize(key.Length))
                return false;
            ds.Key = key;
            return true;
        }

        private static string NoOutputCreate(ClParam param)
        {
            return string.Format(TextResources.NoOutputCreate, param.Key);
        }

        private static string NoEntryExtract(ClParam param)
        {
            return string.Format(TextResources.NoEntryExtract, param.Key);
        }

        private static bool OverwritePrompt(ClParam interactive, ClParam overwrite, ClParam skipexist, ref string filename)
        {
            if (skipexist.IsSet || (!overwrite.IsSet && interactive.IsSet && OverwritePrompt(ref filename)))
            {
                Console.WriteLine(TextResources.OverwriteSkip);
                return true;
            }

            return false;
        }

        private static int Return(int value, ClParam interactive)
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
    }
}
