using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace OpenDayDialogue
{
    class Token
    {
        public enum TokenType
        {
            Indent,
            Dedent,
            Keyword,
            Number,
            Colon,
            Identifier,
            VariableIdentifier,
            String,
            Equals,
            CompareOperator,
            BinaryOperator,
            OpenParen,
            CloseParen,
            EndOfLine,
            True,
            False,
            Undefined,
            Comma,
            UnaryMinus,
            UnaryInvert
        }

        public TokenType type;
        public string content;
        public int line;

        public int paramCount = 0; // parser use
    }

    static class Lexer
    {
        private static bool CheckChars1(this string s, ref int pos, char c1)
        {
            if (pos >= s.Length)
                return false;
            return s[pos] == c1;
        }

        private static bool CheckChars1ReturnVal(this string s, ref int pos, char c1, ref string o)
        {
            if (pos >= s.Length)
                return false;
            bool success = (s[pos] == c1);
            if (success)
                o += c1;
            return success;
        }

        private static bool CheckChars2ReturnVal(this string s, ref int pos, char c1, char c2, ref string o)
        {
            if (pos + 1 >= s.Length)
                return false;
            bool success = (s[pos] == c1 && s[pos + 1] == c2);
            if (success)
            {
                o += c1;
                o += c2;
            }
            return success;
        }

        private static bool CheckChars2(this string s, ref int pos, char c1, char c2)
        {
            if (pos + 1 >= s.Length)
                return false;
            return s[pos] == c1 && s[pos + 1] == c2;
        }

        private static bool CheckChars3(this string s, ref int pos, char c1, char c2, char c3)
        {
            if (pos + 2 >= s.Length)
                return false;
            return s[pos] == c1 && s[pos + 1] == c2 && s[pos + 2] == c3;
        }

        private static bool CheckChars4(this string s, ref int pos, char c1, char c2, char c3, char c4)
        {
            if (pos + 3 >= s.Length)
                return false;
            return s[pos] == c1 && s[pos + 1] == c2 && s[pos + 2] == c3 && s[pos + 3] == c4;
        }

        private static bool IsDelimeter(char c)
        {
            return c == ' ' || c == ':' || c == '=' || c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '^' || c == '>' || c == '<' || c == '(' || c == ')' || c == '!';
        }

        private static string ReadSingleWord(this string s, ref int pos)
        {
            string build = "";
            while (pos < s.Length && !Char.IsWhiteSpace(s[pos]) && !IsDelimeter(s[pos]))
            {
                build += s[pos++];
            }
            return build;
        }

        private static int SkipWhiteSpace(this string s, ref int pos)
        {
            int count = 0;
            while (pos < s.Length && Char.IsWhiteSpace(s[pos]))
            {
                pos++;
                count++;
            }
            return count;
        }

        private static bool IsNumber(ref string s)
        {
            if (s.Length == 0)
                return false;
            bool dot = false;
            foreach (char c in s)
            {
                if ((c != '.' && !Char.IsDigit(c)) || (dot && c == '.'))
                    return false;
                if (c == '.')
                    dot = true;
            }
            return true;
        }

        private static bool IsIdentifier(ref string s)
        {
            if (s.Length == 0)
                return false;
            if (s[0] != '@' && !Char.IsLetter(s[0]))
                return false;
            bool at = false;
            foreach (char c in s)
            {
                if (c != '.' && c != '_' && (!at && c != '@') && !Char.IsLetterOrDigit(c))
                    return false;
                if (c == '@')
                    at = true;
            }
            return true;
        }

        public static List<Token> LexString(string input)
        {
            List<Token> tokens = new List<Token>();
            input = input.Replace("\r", "");
            string[] lines = input.Split('\n');
            int currentIndent = 0;
            for (int line = 0; line < lines.Length; line++)
            {
                string txt = lines[line];
                int pos = 0;

                // Check for preprocessor statement: must be at beginning of line
                if (txt.CheckChars1(ref pos, '#'))
                {
                    pos++;
                    string type = txt.ReadSingleWord(ref pos);
                    if (type == "language")
                    {
                        pos++;
                        Application.programLanguage = txt.ReadSingleWord(ref pos);
                    } else if (type == "include")
                    {
                        pos++;
                        if (txt.CheckChars1(ref pos, '"'))
                        {
                            pos++;
                            string str = "";
                            while (pos < txt.Length && txt[pos] != '"')
                            {
                                if (txt[pos] == '\\')
                                {
                                    pos++;
                                    if (pos < txt.Length)
                                    {
                                        switch (txt[pos])
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
                                                str += txt[pos];
                                                break;
                                        }
                                    }
                                }
                                else
                                    str += txt[pos];
                                pos++;
                            }
                            pos++;
                            if (Application.allFiles.Contains(str))
                                throw new LexerException("Cannot include an already included file!");
                            Application.files.Enqueue(str);
                            Application.allFiles.Add(str);
                        }
                        else throw new LexerException("Invalid preprocessor argument.", line);
                    } else
                    {
                        throw new LexerException("Invalid preprocessor type.", line);
                    }
                    continue;
                }

                // Check for indents/dedents
                int indentCount = 0;
                while (txt.CheckChars1(ref pos, '\t'))
                {
                    pos++;
                    indentCount++;
                }
                while (txt.CheckChars4(ref pos, ' ', ' ', ' ', ' '))
                {
                    pos += 4;
                    indentCount++;
                }
                if (indentCount > currentIndent)
                {
                    int newIndent = indentCount;
                    while (indentCount-- > currentIndent)
                    {
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.Indent,
                            line = line
                        });
                    }
                    currentIndent = newIndent;
                } else if (indentCount < currentIndent)
                {
                    int newIndent = indentCount;
                    while (indentCount++ < currentIndent)
                    {
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.Dedent,
                            line = line
                        });
                    }
                    currentIndent = newIndent;
                }
                
                // Loop through characters, get tokens
                for (; pos < txt.Length;)
                {
                    txt.SkipWhiteSpace(ref pos);

                    // Ignore comments
                    if (txt.CheckChars2(ref pos, '/', '/'))
                    {
                        break;
                    }

                    // Colon
                    if (txt.CheckChars1(ref pos, ':'))
                    {
                        pos++;
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.Colon,
                            line = line
                        });
                    }

                    // Comma
                    if (txt.CheckChars1(ref pos, ','))
                    {
                        pos++;
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.Comma,
                            line = line
                        });
                    }

                    // Compare operator
                    string op = "";
                    if (txt.CheckChars2ReturnVal(ref pos, '=', '=', ref op)
                     || txt.CheckChars2ReturnVal(ref pos, '!', '=', ref op)
                     || txt.CheckChars2ReturnVal(ref pos, '>', '=', ref op)
                     || txt.CheckChars2ReturnVal(ref pos, '<', '=', ref op)
                     || txt.CheckChars2ReturnVal(ref pos, '&', '&', ref op)
                     || txt.CheckChars2ReturnVal(ref pos, '|', '|', ref op)
                     || txt.CheckChars2ReturnVal(ref pos, '^', '^', ref op)
                     || txt.CheckChars1ReturnVal(ref pos, '>', ref op)
                     || txt.CheckChars1ReturnVal(ref pos, '<', ref op)
                     || txt.CheckChars1ReturnVal(ref pos, '!', ref op))
                    {
                        pos += op.Length;
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.CompareOperator,
                            line = line,
                            content = op
                        });
                    }

                    // Binary operator
                    string bop = "";
                    if (txt.CheckChars1ReturnVal(ref pos, '+', ref bop)
                     || txt.CheckChars1ReturnVal(ref pos, '-', ref bop)
                     || txt.CheckChars1ReturnVal(ref pos, '*', ref bop)
                     || txt.CheckChars1ReturnVal(ref pos, '/', ref bop)
                     || txt.CheckChars1ReturnVal(ref pos, '%', ref bop))
                    {
                        pos += bop.Length;
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.BinaryOperator,
                            line = line,
                            content = bop
                        });
                    }

                    // Open parenthesis
                    if (txt.CheckChars1(ref pos, '('))
                    {
                        pos++;
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.OpenParen,
                            line = line
                        });
                    }

                    // Close parenthesis
                    if (txt.CheckChars1(ref pos, ')'))
                    {
                        pos++;
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.CloseParen,
                            line = line
                        });
                    }

                    // Equals
                    if (txt.CheckChars1(ref pos, '='))
                    {
                        pos++;
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.Equals,
                            line = line
                        });
                    }

                    // String
                    if (txt.CheckChars1(ref pos, '"'))
                    {
                        pos++;
                        string str = "";
                        while (pos < txt.Length && txt[pos] != '"')
                        {
                            if (txt[pos] == '\\')
                            {
                                pos++;
                                if (pos < txt.Length)
                                {
                                    switch (txt[pos])
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
                                            str += txt[pos];
                                            break;
                                    }
                                }
                            } else
                                str += txt[pos];
                            pos++;
                        }
                        pos++;

                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.String,
                            line = line,
                            content = str
                        });
                    }

                    // Identifiers, keywords, numbers, etc.
                    string word = txt.ReadSingleWord(ref pos);
                    if (word != "")
                    {
                        // Check for number
                        if (IsNumber(ref word))
                        {
                            tokens.Add(new Token()
                            {
                                type = Token.TokenType.Number,
                                line = line,
                                content = word
                            });
                        } else if (IsIdentifier(ref word))
                        {
                            switch (word)
                            {
                                case "definitions":
                                case "namespace":
                                case "scene":
                                case "if":
                                case "choice":
                                case "else":
                                    tokens.Add(new Token()
                                    {
                                        type = Token.TokenType.Keyword,
                                        line = line,
                                        content = word
                                    });
                                    break;
                                case "true":
                                    tokens.Add(new Token()
                                    {
                                        type = Token.TokenType.True,
                                        line = line
                                    });
                                    break;
                                case "false":
                                    tokens.Add(new Token()
                                    {
                                        type = Token.TokenType.False,
                                        line = line
                                    });
                                    break;
                                case "undefined":
                                    tokens.Add(new Token()
                                    {
                                        type = Token.TokenType.Undefined,
                                        line = line
                                    });
                                    break;
                                default:
                                    tokens.Add(new Token()
                                    {
                                        type = Token.TokenType.Identifier,
                                        line = line,
                                        content = word
                                    });
                                    break;
                            }
                        } else
                        {
                            if (word[0] == '$')
                            {
                                string noPrefix = word.Remove(0, 1);
                                if (IsIdentifier(ref noPrefix))
                                {
                                    tokens.Add(new Token()
                                    {
                                        type = Token.TokenType.VariableIdentifier,
                                        line = line,
                                        content = noPrefix
                                    });
                                }
                            }
                        }
                    }

                    // End of line
                    if (pos + 1 > txt.Length)
                    {
                        tokens.Add(new Token()
                        {
                            type = Token.TokenType.EndOfLine,
                            line = line
                        });
                        if (line + 1 >= lines.Length)
                        {
                            while (currentIndent-- > 0)
                            {
                                tokens.Add(new Token()
                                {
                                    type = Token.TokenType.Dedent,
                                    line = line
                                });
                            }
                        }
                    }
                }
            }
            return tokens;
        }
    }

    [Serializable]
    internal class LexerException : Exception
    {
        public LexerException()
        {
        }

        public LexerException(string message) : base("Lexer error: " + message)
        {
        }

        public LexerException(string message, int line) : base("Lexer error at line " + (line + 1).ToString() + ": " + message)
        {
        }

        public LexerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected LexerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
