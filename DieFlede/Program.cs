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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

            ClParamFlag encAes = new ClParamFlag(parser, TextResources.HelpMAes, '\0', "AES");
            encAes.MutualExclusives.Add(extract);
            extract.OtherMessages.Add(encAes, NoEntryExtract);

            ClParamFlag hide = new ClParamFlag(parser, TextResources.HelpMHide, '\0', "hide", TextResources.PNameHide);
            extract.MutualExclusives.Add(hide);
            extract.OtherMessages.Add(hide, NoEntryExtract);

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

                    if (encAes.IsSet)
                    {
                        if (!interactive.IsSet)
                        {
                            Console.Error.WriteLine(TextResources.EncryptionNoOpts);
                            ShowHelp(clParams, false);
                            return Return(-1, interactive);
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

                            if (CreateEncrypted(encAes, out encFormat, out ssPassword))
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
                        if (CreateEncrypted(encAes, out encFormat, out ssPassword))
                            return -4;

                        using (FileStream fs = File.OpenWrite(archiveFile.Value))
                        using (DieFledermauZArchive archive = new DieFledermauZArchive(fs, hide.IsSet ? encFormat : MausEncryptionFormat.None))
                        {
                            if (hash.Value.HasValue)
                                archive.HashFunction = hash.Value.Value;
                            if (hide.IsSet)
                                archive.Password = ssPassword;

                            for (int i = 0; i < fileInfos.Count; i++)
                            {
                                var curInfo = fileInfos[i];

                                if (verbose.IsSet)
                                    Console.WriteLine(curInfo.Name);

                                DieFledermauZArchiveEntry entry = archive.Create(curInfo.Name, compFormat, hide.IsSet ? MausEncryptionFormat.None : encFormat);
                                if (encAes.IsSet && !hide.IsSet)
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

                        try
                        {
                            dz.Decrypt();
                        }
                        catch (CryptographicException)
                        {
                            Console.Error.WriteLine(TextResources.EncryptedBadKey);
                            return Return(-4, interactive);
                        }
                    }

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
                            if (DoFailDecrypt(curEntry, interactive, i, ref ssPassword) || !MatchesRegexAny(matches, curEntry.Path))
                                continue;

                            Console.WriteLine(GetName(i, curEntry));

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
            catch (CryptographicException)
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

        private static bool CreateEncrypted(ClParamFlag encAes, out MausEncryptionFormat encFormat, out string ssPassword)
        {
            encFormat = MausEncryptionFormat.None;
            if (encAes.IsSet) //Only true if Interactive is also true
            {
                encFormat = MausEncryptionFormat.Aes;

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

#if DEBUG
        private static void GoThrow(Exception e)
        {
            Console.Error.WriteLine("Throw? Y/N> ");
            if (Console.ReadKey().Key == ConsoleKey.Y)
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
                    catch (CryptographicException)
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
    }
}
