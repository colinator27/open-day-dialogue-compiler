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

            Console.WriteLine("Completed! Instructions:\n{0}", c.GetInstructionsString());
            Console.ReadKey();
        }
    }
}
