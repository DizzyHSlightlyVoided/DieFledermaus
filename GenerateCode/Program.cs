using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GenerateCode
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 0)
                throw new ApplicationException("Input file needed.");

            string inPath = Path.GetFullPath(args[0]);

            if (!File.Exists(inPath))
                throw new FileNotFoundException("The input file was not found.", inPath);

            string outPath;

            if (args.Length == 1)
                outPath = Path.ChangeExtension(inPath, ".out.cs");
            else
            {
                outPath = Path.GetFullPath(args[1]);
                if (outPath.Equals(inPath, StringComparison.Ordinal))
                    throw new ApplicationException("Source file is the same as the destination file!");
            }

            using (MemoryStream buffer = new MemoryStream())
            {
                using (StreamReader reader = new StreamReader(inPath, Encoding.UTF8, true))
                using (StreamWriter writer = new StreamWriter(buffer, new UTF8Encoding(true, true), 8192, true))
                {
                    writer.NewLine = "\r\n";

                    Regex typeofRegex = new Regex(@"typeof\(([^\)]+)\)\.Assembly", RegexOptions.CultureInvariant);

                    string curLine;
                    while ((curLine = reader.ReadLine()) != null)
                    {
                        const string sys = "using System;";

                        int dex = curLine.IndexOf(sys);

                        if (dex >= 0)
                        {
                            writer.WriteLine(curLine);
                            writer.WriteLine(curLine.Substring(0, dex + 12) + ".Reflection;");
                        }
                        else writer.WriteLine(typeofRegex.Replace(curLine, "typeof($1).GetTypeInfo().Assembly"));
                    }
                }

                buffer.Seek(0, SeekOrigin.Begin);
                using (FileStream outStream = File.Create(outPath))
                    buffer.CopyTo(outStream);
            }

            Console.WriteLine("Successfully generated file: {0}", outPath);
            return 0;
        }
    }
}
