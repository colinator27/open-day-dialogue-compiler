﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Mono.Options;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenDayDialogue
{
    class Application
    {
        /// <summary>
        /// The version of the binary file that gets exported.
        /// </summary>
        public const uint Version = 4;

        public static string programLanguage = "unknown";

        /// <summary>
        /// The remaining files to compile
        /// </summary>
        public static Queue<string> files = new Queue<string>();

        /// <summary>
        /// The list of all files that have been or will be compiled.
        /// </summary>
        public static List<string> allFiles = new List<string>();

        /// <summary>
        /// The current file being compiled.
        /// </summary>
        public static string currentFile = "";

        /// <summary>
        /// Whether or not to generate translation files
        /// </summary>
        public static bool generateTranslations = false;

        /// <summary>
        /// Whether or not to include/exclude Values in the code for translations
        /// </summary>
        public static bool excludeValues = false;

        /// <summary>
        /// Whether or not to apply a translation file.
        /// </summary>
        public static bool applyTranslations = false;

        /// <summary>
        /// Whether or not to ignore the hash of the file when applying translations
        /// </summary>
        public static bool translationIgnoreHash = false;

        public static Dictionary<string/*filename*/, Tuple<string/*file hash*/, Dictionary<string/*symbol name*/, List<string>/*strings*/>>> genTranslations;
        public static Dictionary<string/*filename*/, Tuple<string/*file hash*/, Dictionary<string/*symbol name*/, Queue<string>/*strings*/>>> queuedTranslations;

        /// <summary>
        /// Whether or not to log item names when an error is logged.
        /// </summary>
        public static bool errorsLogItems = false;

        /// <summary>
        /// Whether or not to emit debugging instructions when compiling.
        /// </summary>
        public static bool emitDebugInstructions = false;

        protected static void Main(string[] args)
        {
            string sourceFileName = "", exportFileName = "";
            bool showHelp = false;
            bool invalidParameters = false;
            bool showInstructions = false;

            OptionSet options = new OptionSet
            {
                { "s|source=", "The input source {file} name/path.", x => sourceFileName = x },
                { "e|export=", "The export binary {file} name/path. Used in conjunction with '--source' option.", x => exportFileName = x },
                { "d|debug", "Will emit debug instructions when compiling, useful for debugging with an interpreter.", x => emitDebugInstructions = (x != null) },
                { "make-translations", "Generate translation files as compiling happens. Outputs to the same directory as source files, with extension '.opdat'.", x => generateTranslations = (x != null) },
                { "exclude-values", "Exclude values/commands AND expressions when generating translations (has no effect when applying the translation files).", x => excludeValues = (x != null) },
                { "apply-translations", "Will apply translation files if they are found with the source code files. They must be the source file's name followed by '.opdat'.", x => applyTranslations = (x != null) },
                { "ignore-hash", "When applying translation files, ignore the original file hash. Warning: This can be risky.", x => translationIgnoreHash = (x != null) },
                { "show-instructions", "Show the final list of instructions when the program finishes compiling.", x => showInstructions = (x != null) },
                { "err-log-items", "When an error gets logged, it will include the item's name if possible.", x => errorsLogItems = (x != null) },
                { "h|help", "Show this help menu.", h => showHelp = (h != null) }
            };

            if (showHelp)
            {
                Console.WriteLine("Usage: {0} <options>", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("Options:");
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

            Console.WriteLine("Preparing methods...");
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic |
                                                       BindingFlags.Public | BindingFlags.Instance |
                                                       BindingFlags.Static))
                {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                }
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (generateTranslations)
            {
                genTranslations = new Dictionary<string, Tuple<string, Dictionary<string, List<string>>>>();
            }
            if (applyTranslations)
            {
                queuedTranslations = new Dictionary<string, Tuple<string, Dictionary<string, Queue<string>>>>();
            }

            sourceFileName = new FileInfo(sourceFileName).FullName;
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
                    genTranslations.Add(filename, new Tuple<string, Dictionary<string, List<string>>>(hash, new Dictionary<string, List<string>>()));
                }
                if (applyTranslations)
                {
                    // Load translation data for the file
                    if (File.Exists(filename + ".opdat"))
                    {
                        Console.WriteLine("Calculating SHA256 of text and loading data from translation file...");
                        hash = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLower();
                        string[] lines;
                        try
                        {
                            lines = File.ReadAllLines(filename + ".opdat");
                        } catch (Exception e)
                        {
                            Console.WriteLine("Failed to open translation file \"{0}\".\nMessage: {1}", filename + ".opdat", e.Message);
                            return;
                        }
                        var t = TranslationManager.GetTranslationFromFileLines(hash, filename, lines);
                        if (t.Item1 == "" || t.Item2 == null)
                            return;
                        queuedTranslations.Add(filename, t);
                    } else
                    {
                        Console.WriteLine("Found no translation file at {0}", filename + ".opdat");
                    }
                }

                // Create a token stream from input file
                Console.WriteLine("Lexing...");
                List<Token> tokens = Lexer.LexString(text);
                if (!Errors.CanContinue())
                {
                    Console.WriteLine("Compilation stopped due to errors.");
                    Console.WriteLine(Errors.GetErrors());
                    return;
                }

                // Parse the token stream
                Console.WriteLine("Parsing...");
                Parser p;
                try
                {
                    p = new Parser(tokens);
                }
                catch (FatalErrorException e)
                {
                    Console.WriteLine("Fatal code error: " + e.Message);
                    Console.WriteLine("Compilation stopped due to errors.");
                    return;
                }

                if (!Errors.CanContinue())
                {
                    Console.WriteLine("Compilation stopped due to errors.");
                    Console.WriteLine(Errors.GetErrors());
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

                if (!Errors.CanContinue())
                {
                    Console.WriteLine("Compilation stopped due to errors.");
                    Console.WriteLine(Errors.GetErrors());
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
                foreach (var pair in genTranslations)
                {
                    Console.WriteLine("Writing translation file at {0}", pair.Key + ".opdat");

                    string text = TranslationManager.GetTranslationText(pair);

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

            watch.Stop();

            Console.WriteLine("Completed in {0} ms.", watch.ElapsedMilliseconds);

            if (Errors.errors.Count > 0)
            {
                Console.WriteLine(Errors.GetErrors());
            }

            Console.WriteLine("\n\nStatistics:\n\nInstruction count: {0}\nUnique command count: {1}" +
                              "\nDefinition count: {2}\nScene count: {3}\nUnique string count: {4}" +
                              "\nUnique value count: {5}", 
                c.program.instructions.Count, c.program.commandTable.Count, c.program.definitions.Count,
                c.program.scenes.Count, c.program.stringEntries.Count, c.program.values.Count);

            if (showInstructions)
            {
                Console.WriteLine(string.Format("\n\nInstructions:\n\n{0}", c.GetInstructionsString()));
            }
        }
    }
}
