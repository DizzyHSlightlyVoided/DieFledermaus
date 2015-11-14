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
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
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

            string entryFile = null, entryParam = null, archiveFile = null, outFile = null, outParam = null;

            bool _failed = false, _help = false, extract = false;

            for (int i = 0; !_failed && i < args.Length; i++)
            {
                string curArg = args[i];

                if (curArg.StartsWith("--"))
                {
                    switch (CheckFlag(curArg, "--help", true))
                    {
                        case FlagResult.Failed:
                            _failed = true;
                            continue;
                        case FlagResult.Yes:
                            _help = true;
                            continue;
                    }

                    switch (CheckFlag(curArg, "--extract", true))
                    {
                        case FlagResult.Failed:
                            _failed = true;
                            continue;
                        case FlagResult.Yes:
                            _failed = ExtractSwitch(entryParam, ref entryFile, ref outFile);
                            extract = true;
                            continue;
                    }

                    if (CheckFileArg(curArg, ref _failed, ref archiveFile, "--archive", "--file"))
                        continue;

                    string curName;
                    if (CheckFileArg(curArg, ref _failed, ref entryFile, out curName, "--entry"))
                    {
                        if (extract && !_failed)
                        {
                            _failed = true;
                            Console.WriteLine(TextResources.NoEntryExtract, curName);
                        }
                        else if (entryParam == null) entryParam = curName;
                        continue;
                    }

                    if (CheckFileArg(curArg, ref _failed, ref outFile, out curName, "--output", "--out"))
                    {
                        if (outParam == null) outParam = curName;
                        continue;
                    }

                    int dex = curArg.IndexOf('=');

                    if (dex >= 0)
                        curArg = curArg.Substring(0, dex);

                    Console.Error.WriteLine(TextResources.ParamUnknown, curArg);
                    continue;
                }

                if (curArg.StartsWith("-"))
                {
                    char prevLetter = '-', curLetter;
                    for (int j = 1; !_failed && j < curArg.Length; j++, prevLetter = curLetter)
                    {
                        curLetter = curArg[j];

                        if (curLetter == '=')
                        {
                            switch (prevLetter)
                            {
                                case '-':
                                    Console.WriteLine(TextResources.ParamInvalid, curArg);
                                    break;
                                case 'h':
                                case 'x':
                                    Console.WriteLine(TextResources.ParamNoArg, "-" + prevLetter);
                                    break;
                                case 'o':
                                case 'e':
                                case 'f':
                                    break;
                                default:
                                    _failed = true;
                                    break;
                            }
                            break;
                        }

                        switch (curLetter)
                        {
                            case '-':
                                curArg = curArg.Substring(j);
                                continue;
                            case 'h':
                                _help = true;
                                continue;
                            case 'x':
                                _failed = ExtractSwitch(entryParam, ref entryFile, ref outFile);
                                extract = true;
                                continue;
                            case 'o':
                                _failed = CheckSingleLetterFile(args, curArg, j, ref i, "-o", ref outFile);
                                if (outParam == null) outParam = "-o";
                                continue;
                            case 'e':
                                _failed = CheckSingleLetterFile(args, curArg, j, ref i, "-e", ref entryFile);
                                if (extract && !_failed)
                                {
                                    _failed = true;
                                    Console.WriteLine(TextResources.NoEntryExtract, "-e");
                                }
                                else if (entryParam == null) entryParam = "-e";
                                continue;
                            case 'f':
                                _failed = CheckSingleLetterFile(args, curArg, j, ref i, "-f", ref archiveFile);
                                continue;
                        }
                        Console.Error.WriteLine(TextResources.ParamUnknown, '-' + curLetter);
                    }

                    continue;
                }

                if (extract)
                    CheckFilename(null, curArg, ref outFile);
                else
                    CheckFilename(null, curArg, ref entryFile);
            }

            if (!_failed)
            {
                if (extract)
                {
                    if (outFile != null)
                    {
                        if (archiveFile == null)
                        {
                            Console.Error.WriteLine(TextResources.ExtractNoArchive);
                            _failed = true;
                        }
                        else if (!File.Exists(archiveFile))
                        {
                            Console.Error.WriteLine(TextResources.FileNotFound, archiveFile);
                            return -1;
                        }
                    }
                }
                else if ((args.Length > 0 && !_help) || archiveFile != null || outFile != null)
                {
                    if (outFile != null)
                    {
                        Console.Error.WriteLine(TextResources.NoOutputExtract, outParam);
                    }

                    if (entryFile == null)
                    {
                        Console.Error.WriteLine(TextResources.CreateNoEntry);
                        _failed = true;
                    }
                    else if (!File.Exists(entryFile))
                    {
                        Console.Error.WriteLine(TextResources.FileNotFound, entryFile);
                        return -1;
                    }
                }
            }

            if (_help || args.Length == 0 || _failed)
            {
                Console.WriteLine(TextResources.Usage);

                StringBuilder commandName = new StringBuilder();
                if (Type.GetType("Mono.Runtime") != null)
                    commandName.Append("mono ");
                commandName.Append(Path.GetFileName(typeof(Program).Assembly.Location));

                Console.WriteLine(TextResources.HelpCompress);
                Console.WriteLine(" > {0} -f [{1}.maus] [{2}]", commandName, TextResources.HelpArchive, TextResources.HelpInput);
                Console.WriteLine();
                Console.WriteLine(TextResources.HelpDecompress);
                Console.WriteLine(" > {0} -xf [{1}.maus] [{2}]", commandName, TextResources.HelpArchive, TextResources.HelpOutput);
                Console.WriteLine();

                if (_help)
                {
                    //TODO: Extended help
                }

                if (_failed)
                    return -1;
            }

            const string mausExt = ".maus";
            const int extLen = 5;
#if !DEBUG
            try
#endif
            {
                if (extract)
                {
                    using (FileStream fs = File.OpenRead(archiveFile))
                    using (DieFledermausStream ds = new DieFledermausStream(fs, CompressionMode.Decompress))
                    {
                        if (outFile == null)
                        {
                            if (ds.Filename != null)
                            {
                                outFile = Path.GetFullPath(ds.Filename);
                            }
                            else if (archiveFile.Length > extLen && mausExt.Equals(Path.GetExtension(archiveFile), StringComparison.OrdinalIgnoreCase))
                            {
                                outFile = archiveFile.Substring(0, archiveFile.Length - extLen);
                            }
                            else
                            {
                                outFile = archiveFile + ".out";
                                Console.WriteLine(TextResources.RenameExtract, outFile);
                            }
                        }

                        if (File.Exists(outFile))
                        {
                            if (outFile.Equals(archiveFile))
                                Console.WriteLine(TextResources.OverwriteSameFile, archiveFile);
                            else
                                Console.WriteLine(TextResources.OverwriteAlert, outFile);

                            bool notFound = true, overwriting = false;
                            do
                            {
                                Console.Write(TextResources.OverwritePrompt + "> ");

                                var line = Console.ReadLine().Trim();
                                const string oYes = "yes", oNo = "no";

                                if (string.IsNullOrEmpty(line)) continue;

                                if (oYes.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith(oYes, StringComparison.OrdinalIgnoreCase) ||
                                    TextResources.OverYes.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith(TextResources.OverNo, StringComparison.OrdinalIgnoreCase))
                                {
                                    notFound = false;
                                }
                                else if (oNo.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith(oNo, StringComparison.OrdinalIgnoreCase) ||
                                    TextResources.OverNo.StartsWith(line, StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith(TextResources.OverNo, StringComparison.OrdinalIgnoreCase))
                                {
                                    return 0;
                                }
                            }
                            while (notFound);
                        }

                        using (FileStream outStream = File.Create(outFile))
                            ds.CopyTo(outStream);
                        return 0;
                    }
                }

                using (FileStream fs = File.OpenRead(entryFile))
                {
                    if (archiveFile == null)
                        archiveFile = entryFile + mausExt;

                    using (Stream arStream = File.Create(archiveFile))
                    using (DieFledermausStream ds = new DieFledermausStream(arStream, CompressionMode.Compress))
                    {
                        ds.Filename = Path.GetFileName(archiveFile);
                        fs.CopyTo(ds);
                        return 0;
                    }
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return e.HResult;
            }
#endif
        }

        private enum FlagResult
        {
            Yes,
            NoMatch,
            Failed,
            ValueSet = Failed
        }

        private static bool ExtractSwitch(string entryParam, ref string entryFile, ref string outFile)
        {
            if (entryFile == null)
                return false;

            if (string.IsNullOrEmpty(entryParam))
            {
                outFile = entryFile;
                entryFile = null;
                return false;
            }

            Console.Error.WriteLine(TextResources.NoEntryExtract, entryParam);
            return true;
        }

        private static bool CheckFileArg(string curArg, ref bool _failed, ref string filename, out string name, params string[] names)
        {
            string curVal;
            switch (CheckVal(curArg, out curVal, out name, names))
            {
                case FlagResult.Failed:
                    _failed = true;
                    return true;
                case FlagResult.Yes:
                    _failed = CheckFilename(name, curVal, ref filename);
                    return true;
            }
            return false;
        }

        private static bool CheckFileArg(string curArg, ref bool _failed, ref string filename, params string[] names)
        {
            string name;
            return CheckFileArg(curArg, ref _failed, ref filename, out name, names);
        }

        private static bool CheckSingleLetterFile(string[] args, string curArg, int j, ref int i, string name, ref string filePath)
        {
            int iNext = i + 1, jNext = j + 1;

            if (jNext == curArg.Length)
            {
                if (iNext == args.Length || args[iNext].StartsWith("-"))
                {
                    Console.Error.WriteLine(TextResources.ParamReqArg, name);
                    return true;
                }

                i += iNext;
                curArg = args[i];
                return CheckFilename(name, curArg, ref filePath);
            }

            if (curArg[jNext] != '=')
            {
                Console.Error.WriteLine(TextResources.ParamReqArg, name);
                return true;
            }

            curArg = curArg.Substring(jNext + 1);
            return CheckFilename(name, curArg, ref filePath);
        }

        private static bool CheckFilename(string curName, string curVal, ref string filename)
        {
            curVal = Path.GetFullPath(curVal);
            if (filename == null)
            {
                filename = curVal;
                return false;
            }

            if (!filename.Equals(curVal, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(curName))
                    Console.Error.WriteLine(TextResources.ParamDupLit, curVal);
                else
                    Console.Error.WriteLine(TextResources.ParamDup, curName, curVal);
                return true;
            }
            return false;
        }

        private static FlagResult CheckFlag(string curArg, string name, bool messageOnFail)
        {
            if (name.Equals(curArg, StringComparison.OrdinalIgnoreCase))
                return FlagResult.Yes;

            if (curArg.StartsWith(name + '=', StringComparison.OrdinalIgnoreCase))
            {
                if (messageOnFail)
                    Console.Error.WriteLine(TextResources.ParamNoArg, name);
                return FlagResult.Failed;
            }

            return FlagResult.NoMatch;
        }

        private static FlagResult CheckVal(string curArg, out string value, out string name, params string[] names)
        {
            foreach (string curName in names)
            {
                var result = CheckVal(curArg, curName, out value);
                if (result != FlagResult.NoMatch)
                {
                    name = curName;
                    return result;
                }
            }
            value = name = null;
            return FlagResult.NoMatch;
        }

        private static FlagResult CheckVal(string curArg, string name, out string value)
        {
            FlagResult result = CheckFlag(curArg, name, false);

            if (result == FlagResult.ValueSet)
            {
                value = curArg.Substring(name.Length + 1);
                return FlagResult.Yes;
            }

            if (result == FlagResult.NoMatch)
            {
                value = null;
                return FlagResult.NoMatch;
            }

            Console.Error.WriteLine(TextResources.ParamNoArg, name);
            value = null;
            return FlagResult.Failed;
        }
    }
}
