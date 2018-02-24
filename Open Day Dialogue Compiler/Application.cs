using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDayDialogue
{
    class Application
    {
        public static string programLanguage = "unknown";
        public static Queue<string> files = new Queue<string>();
        public static List<string> allFiles = new List<string>();

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Proper syntax: [application.exe] <source_file_main> <export_binary_file>");
                return;
            }

            files.Enqueue(args[0]);
            allFiles.Add(args[0]);

            Compiler c = new Compiler();
            while (files.Count > 0)
            {
                string filename = files.Dequeue();
                string text;
                try
                {
                    text = File.ReadAllText(filename);
                } catch (Exception e)
                {
                    Console.WriteLine("Failed to open file \"{0}\". Exception: {1}", filename, e.Message);
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("Process file: {0}", filename);
                Console.WriteLine("Lexing...");
                List<Token> tokens;
                try
                {
                    tokens = Lexer.LexString(text);
                } catch (LexerException e)
                {
                    Console.WriteLine(e.Message);
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("Parsing...");
                Parser p;
                try
                {
                    p = new Parser(tokens);
                } catch (ParserException e)
                {
                    Console.WriteLine(e.Message);
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("Generating code...");
                try
                {
                    c.GenerateCode(p.block);
                } catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.ReadKey();
                    return;
                }
            }

            Console.WriteLine("Writing binary...");
            try
            {
                using (FileStream fs = new FileStream(args[1], FileMode.Create))
                {
                    BytecodeWriter bw = new BytecodeWriter(c.program);
                    bw.Write(fs);
                }
            } catch (Exception e)
            {
                Console.WriteLine("Failed to write binary: " + e.Message);
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Completed!");
            Console.WriteLine("\n\nStatistics:\n\nInstruction count: {0}\nUnique command count: {1}" +
                              "\nDefinition count: {2}\nScene count: {3}\nUnique string count: {4}" +
                              "\nUnique value count: {5}", 
                c.program.instructions.Count, c.program.commandTable.Count, c.program.definitions.Count,
                c.program.scenes.Count, c.program.stringEntries.Count, c.program.values.Count);
            Console.ReadKey();
        }
    }
}
