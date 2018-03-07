using System;
using System.Collections.Generic;

namespace OpenDayDialogue
{
    public static class TranslationManager
    {
        public static string GetTranslationText(KeyValuePair<string, Tuple<string, Dictionary<string, List<string>>>> pair)
        {
            string text = "";

            // Header
            text += "~" + pair.Value.Item1 + "\n";
            text += "!" + (Application.excludeValues ? "E" : "e") + "\n\n";

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

            return text;
        }

        public static Tuple<string, Dictionary<string, Queue<string>>> GetTranslationFromFileLines(string inputSHA, string filename, string[] lines)
        {
            Console.WriteLine("Parsing translation file...");

            if (lines.Length >= 2 && lines[0].StartsWith("~", StringComparison.CurrentCulture))
            {
                lines[0] = lines[0].Remove(0, 1).Trim();
                if (!Application.translationIgnoreHash)
                {
                    if (inputSHA != lines[0])
                    {
                        Console.WriteLine("Translation file version does not match the source file version; hash mismatch.");
                        return new Tuple<string, Dictionary<string, Queue<string>>>("", null);
                    }
                }
            } else
            {
                Console.WriteLine("Invalid translation file.");
                return new Tuple<string, Dictionary<string, Queue<string>>>("", null);
            }

            if (lines[1].StartsWith("!", StringComparison.CurrentCulture))
            {
                lines[1] = lines[1].Remove(0, 1).Trim();
                Application.excludeValues = (lines[1] == "E");
            } else
            {
                Console.WriteLine("Invalid translation file.");
                return new Tuple<string, Dictionary<string, Queue<string>>>("", null);
            }

            var tuple = new Tuple<string, Dictionary<string, Queue<string>>>
                            (filename, new Dictionary<string, Queue<string>>());

            string currentItem = "";

            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line == "")
                    continue;
                switch (line[0])
                {
                    case '=':
                        currentItem = line.Remove(0, 1);
                        tuple.Item2.Add(currentItem, new Queue<string>());
                        break;
                    case '"':
                        string str = "";
                        int pos = 1;
                        while (pos < line.Length && line[pos] != '"')
                        {
                            if (line[pos] == '\\')
                            {
                                pos++;
                                if (pos < line.Length)
                                {
                                    switch (line[pos])
                                    {
                                        case 'n':
                                            str += '\n';
                                            break;
                                        case 'r':
                                            str += '\r';
                                            break;
                                        case 't':
                                            str += '\t';
                                            break;
                                        default:
                                            str += line[pos];
                                            break;
                                    }
                                }
                            }
                            else
                                str += line[pos];
                            pos++;
                        }
                        tuple.Item2[currentItem].Enqueue(str);
                        break;
                    case '#': // comment
                        break;
                    default:
                        Console.WriteLine("Invalid translation file.");
                        return new Tuple<string, Dictionary<string, Queue<string>>>("", null);
                }
            }

            return tuple;
        }
    }
}
