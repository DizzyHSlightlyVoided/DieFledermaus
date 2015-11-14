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

            ClParam help = new ClParam(TextResources.HelpMHelp, 'h', "help", TextResources.PNameHelp);
            ClParam create = new ClParam(TextResources.HelpMCreate, 'c', "create", TextResources.PNameCreate);
            ClParam extract = new ClParam(TextResources.HelpMExtract, 'x', "extract", TextResources.PNameExtract);
            extract.MutualExclusives.Add(create);
            create.MutualExclusives.Add(extract);

            ClParam archiveFile = new ClParam(TextResources.HelpMArchive, TextResources.HelpArchive, 'f', "file", "archive",
                TextResources.PNameFile, TextResources.PNameArchive);
            archiveFile.ConvertValue = Path.GetFullPath;

            ClParam entryFile = new ClParam(TextResources.HelpMEntry, TextResources.HelpInput, 'e', "entry", "input",
                TextResources.PNameEntry, TextResources.PNameInput);
            entryFile.ConvertValue = Path.GetFullPath;
            entryFile.MutualExclusives.Add(extract);
            entryFile.OtherMessages.Add(extract, delegate (ClParam xtr)
            {
                return string.Format(TextResources.NoEntryExtract, entryFile.Key);
            });

            ClParam outFile = new ClParam(TextResources.HelpMOut, TextResources.HelpOutput, 'o', "out", "output",
                TextResources.PNameOut, TextResources.PNameOutput);
            outFile.ConvertValue = Path.GetFullPath;
            outFile.MutualExclusives.Add(create);
            outFile.OtherMessages.Add(create, delegate (ClParam crt)
            {
                return string.Format(TextResources.NoOutputCreate, outFile.Key);
            });
            outFile.MutualExclusives.Add(entryFile);

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

            bool _failed;

            ClParam[] clParams = { create, extract, help, entryFile, archiveFile, outFile };

            if (args.Length == 1 && args[0][0] != '-')
            {
                _failed = false;
                archiveFile.IsSet = true;
                archiveFile.Value = args[0];
                extract.IsSet = true;
            }
            else
            {
                using (ClParser parser = new ClParser(Array.IndexOf(clParams, entryFile), clParams))
                    _failed = parser.Parse(args);
            }
            bool acting = false;

            if (!_failed && args.Length > 0)
            {
                if (extract.IsSet)
                {
                    if (!archiveFile.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.ExtractNoArchive);
                        _failed = true;
                    }
                    else if (!File.Exists(archiveFile.Value))
                    {
                        Console.Error.WriteLine(TextResources.FileNotFound, archiveFile.Value);
                        _failed = true;
                    }
                    else acting = true;
                }
                else if (create.IsSet)
                {
                    if (!entryFile.IsSet)
                    {
                        Console.Error.WriteLine(TextResources.CreateNoEntry);
                        _failed = true;
                    }
                    else acting = true;
                }
                else if (archiveFile.IsSet || entryFile.IsSet || outFile.IsSet)
                {
                    Console.Error.WriteLine(TextResources.RequireAtLeastOne, "-c, -x");
                    _failed = true;
                }
                else if (!help.IsSet)
                {
                    Console.Error.WriteLine(TextResources.RequireAtLeastOne, "-c, -x, --help");
                    _failed = true;
                }
            }

            if (help.IsSet || args.Length == 0 || _failed)
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

                if (!_failed && help.IsSet && !acting)
                {
                    Console.Write('\t');
                    Console.WriteLine(TextResources.Parameters);

                    for (int i = 0; i < clParams.Length; i++)
                    {
                        var curParam = clParams[i];

                        IEnumerable<string> paramList;

                        if (curParam.TakesValue)
                        {
                            paramList = new string[] { string.Concat(curParam.LongNames[0], "=<", curParam.ArgName, ">") }.
                                Concat(new ArraySegment<string>(curParam.LongNames, 1, curParam.LongNames.Length - 1));
                        }
                        else paramList = curParam.LongNames;

                        paramList = paramList.Select((n, index) => "--" + n);
                        if (curParam.ShortName != '\0')
                        {
                            string shortName = "-" + curParam.ShortName;

                            if (curParam.TakesValue)
                                shortName += " <" + curParam.ArgName + ">";

                            paramList = new string[] { shortName }.Concat(paramList);
                        }

                        Console.WriteLine(string.Join(", ", paramList));
                        Console.Write("  ");
                        Console.WriteLine(curParam.HelpMessage);
                        Console.WriteLine();
                    }

                    return 0;
                }
            }

            if (_failed)
                return -1;

            const string mausExt = ".maus";
            const int extLen = 5;
            try
            {
                if (extract.IsSet)
                {
                    using (FileStream fs = File.OpenRead(archiveFile.Value))
                    using (DieFledermausStream ds = new DieFledermausStream(fs, CompressionMode.Decompress))
                    {
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
                            if (outFile.Value.Equals(archiveFile.Value, StringComparison.Ordinal))
                                Console.WriteLine(TextResources.OverwriteSameArchive, archiveFile.Value);
                            else
                                Console.WriteLine(TextResources.OverwriteAlert, outFile.Value);

                            if (OverwritePrompt())
                                return 0;
                        }

                        using (FileStream outStream = File.Create(outFile.Value))
                            ds.CopyTo(outStream);
                        return 0;
                    }
                }

                using (FileStream fs = File.OpenRead(entryFile.Value))
                {
                    if (archiveFile.Value == null)
                        archiveFile.Value = entryFile.Value + mausExt;

                    if (File.Exists(archiveFile.Value))
                    {
                        if (archiveFile.Value.Equals(entryFile.Value, StringComparison.Ordinal))
                            Console.WriteLine(TextResources.OverwriteSameEntry, entryFile.Value);
                        else
                            Console.WriteLine(TextResources.OverwriteAlert, archiveFile.Value);

                        if (!OverwritePrompt())
                            return 0;
                    }

                    using (Stream arStream = File.Create(archiveFile.Value))
                    using (DieFledermausStream ds = new DieFledermausStream(arStream, CompressionMode.Compress))
                    {
                        ds.Filename = Path.GetFileName(entryFile.Value);
                        fs.CopyTo(ds);
                        return 0;
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
#if DEBUG
                Console.Error.WriteLine("Throw? Y/N> ");
                if (Console.ReadKey().Key == ConsoleKey.Y)
                    throw new Exception(e.Message, e);
#endif
                return e.HResult;
            }
        }

        private static bool OverwritePrompt()
        {
            bool notFound = true;
            do
            {
                Console.Write(TextResources.OverwritePrompt + "> ");

                string line = Console.ReadLine().Trim();
                const string oYes = "yes", oNo = "no";

                if (string.IsNullOrEmpty(line))
                    continue;

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
                    return true;
                }
            }
            while (notFound);
            return false;
        }
    }
}
