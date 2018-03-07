using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Mono.Options;

namespace OpenDayDialogue
{
    class Application
    {
        public static string programLanguage = "unknown";
        public static Queue<string> files = new Queue<string>();
        public static List<string> allFiles = new List<string>();
        public static string currentFile = "";
        public static bool generateTranslations = false;
        public static bool excludeValues = false;
        public static Dictionary<string/*filename*/, Tuple<string/*file hash*/, Dictionary<string/*symbol name*/, List<string>/*strings*/>>> translations;

        protected static void Main(string[] args)
        {
            string sourceFileName = "", exportFileName = "";
            bool showHelp = false;
            bool invalidParameters = false;

            OptionSet options = new OptionSet
            {
                { "s|source=", "The input source {file} name/path. Used in conjunction with export.", s => sourceFileName = s },
                { "e|export=", "The export binary {file} name/path.", e => exportFileName = e },
                { "t", "Generate translation files as compiling happens.", g => generateTranslations = (g != null) },
                { "c", "Exclude values/commands when generating translations (has no effect when applying the translation files).", _c => excludeValues = (_c != null) },            
                { "h|help", "Show help menu.", h => showHelp = (h != null) }
            };

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            try
            {
                options.Parse(args);
                if (sourceFileName == "" || (!generateTranslations && exportFileName == ""))
                {
                    invalidParameters = true;
                }
            }
            catch (OptionException)
            {
                invalidParameters = true;
            }

            if (invalidParameters)
            {
                Console.WriteLine("Usage: {0} <options>", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (generateTranslations)
            {
                translations = new Dictionary<string, Tuple<string, Dictionary<string, List<string>>>>();
            }

            files.Enqueue(sourceFileName);
            allFiles.Add(sourceFileName);

            Compiler c = new Compiler();
            while (files.Count > 0)
            {
                string filename = files.Dequeue();
                currentFile = filename;

                // Read text
                string text;
                try
                {
                    text = File.ReadAllText(filename);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to open file \"{0}\".\nMessage: {1}", filename, e.Message);
                    return;
                }

                Console.WriteLine("Process file: {0}", filename);

                string hash = "";
                if (generateTranslations)
                {
                    // Create translation data structure for the file
                    Console.WriteLine("Calculating SHA256 of text and creating translation structures...");
                    hash = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLower();
                    translations.Add(filename, new Tuple<string, Dictionary<string, List<string>>>(hash, new Dictionary<string, List<string>>()));
                }

                // Create a token stream from input file
                Console.WriteLine("Lexing...");
                List<Token> tokens;
                try
                {
                    tokens = Lexer.LexString(text);
                }
                catch (LexerException e)
                {
                    Console.WriteLine(e.Message);
                    return;
                }

                // Parse the token stream
                Console.WriteLine("Parsing...");
                Parser p;
                try
                {
                    p = new Parser(tokens);
                }
                catch (ParserException e)
                {
                    Console.WriteLine(e.Message);
                    return;
                }

                // Generate code
                Console.WriteLine("Generating code...");
                try
                {
                    c.GenerateCode(p.block);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return;
                }
            }

            if (exportFileName != "")
            {
                // Make it harder for people to snoop conversations
                Console.WriteLine("Shuffling strings...");
                c.program.ShuffleStrings();

                Console.WriteLine("Writing binary to {0}", exportFileName);
                try
                {
                    using (FileStream fs = new FileStream(exportFileName, FileMode.Create))
                    {
                        BytecodeWriter bw = new BytecodeWriter(c.program);
                        bw.Write(fs);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to write binary: " + e.Message);
                    return;
                }
            }
            else
            {
                Console.WriteLine("Not writing binary and not shuffling strings; no file specified.");
            }

            if (generateTranslations)
            {
                foreach (var pair in translations)
                {
                    Console.WriteLine("Writing translation file at {0}", pair.Key + ".opdat");

                    string text = "";

                    // Header
                    text += "~" + pair.Value.Item1 + "\n";
                    text += "!" + (excludeValues ? "E" : "e") + "\n\n";

                    // Each item
                    foreach (var pair2 in pair.Value.Item2)
                    {
                        text += "=" + pair2.Key + "\n";
                        foreach (string s in pair2.Value)
                        {
                            text += "\"";
                            foreach (char ch in s)
                            {
                                switch (ch)
                                {
                                    case '\n':
                                        text += "\\n";
                                        break;
                                    case '\r':
                                        text += "\\r";
                                        break;
                                    case '\t':
                                        text += "\\t";
                                        break;
                                    case '"':
                                        text += "\\\"";
                                        break;
                                    default:
                                        text += ch;
                                        break;
                                }
                            }
                            text += "\"\n";
                        }
                        text += "\n";
                    }

                    // Write to file
                    try
                    {
                        File.WriteAllText(pair.Key + ".opdat", text, Encoding.UTF8);
                    } catch (Exception e)
                    {
                        Console.WriteLine("Failed to write translation file with path {0}.", pair.Key + ".opdat");
                        Console.WriteLine("Message: {0}", e.Message);
                        return;
                    }
                }
            }

            Console.WriteLine("Completed!");
            Console.WriteLine("\n\nStatistics:\n\nInstruction count: {0}\nUnique command count: {1}" +
                              "\nDefinition count: {2}\nScene count: {3}\nUnique string count: {4}" +
                              "\nUnique value count: {5}", 
                c.program.instructions.Count, c.program.commandTable.Count, c.program.definitions.Count,
                c.program.scenes.Count, c.program.stringEntries.Count, c.program.values.Count);
        }
    }
}
