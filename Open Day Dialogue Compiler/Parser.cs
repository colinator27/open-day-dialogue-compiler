using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace OpenDayDialogue
{
    abstract class Node
    {
        public Node parent;

        public Node(Node parent, Parser parser)
        {
            this.parent = parent;
        }
    }

    class Statement : Node
    {
        public enum Type
        {
            Block,
            DefinitionGroup,
            Namespace,
            Scene
        }
        public Type type;
        public Block block;
        public DefinitionGroup definitionGroup;
        public Namespace @namespace;
        public Scene scene;

        public Statement(Node parent, Parser parser) : base(parent, parser)
        {
            if (Block.CanParse(parser))
            {
                type = Type.Block;
                block = new Block(this, parser);
            } else if (DefinitionGroup.CanParse(parser))
            {
                type = Type.DefinitionGroup;
                definitionGroup = new DefinitionGroup(this, parser);
            } else if (Namespace.CanParse(parser))
            {
                type = Type.Namespace;
                @namespace = new Namespace(this, parser);
            } else if (Scene.CanParse(parser))
            {
                type = Type.Scene;
                scene = new Scene(this, parser);
            }
            else
            {
                throw new ParserException("Unable to find any statement to parse!");
            }
        }
    }

    class SceneText : Node
    {
        public string text;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.String) && !parser.AreNextTokens(Token.TokenType.String, Token.TokenType.Colon);
        }
        
        public SceneText(Node parent, Parser parser, string content) : base(parent, parser)
        {
            text = content;
        }

        public SceneText(Node parent, Parser parser) : base(parent, parser)
        {
            text = parser.EnsureToken(Token.TokenType.String).content;
            parser.EnsureToken(Token.TokenType.EndOfLine);
        }
    }

    class SceneCommand : Node
    {
        public List<Value> args;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Identifier);
        }

        public SceneCommand(Node parent, Parser parser, List<Value> args) : base(parent, parser)
        {
            this.args = args;
        }

        public SceneCommand(Node parent, Parser parser) : base(parent, parser)
        {
            args = new List<Value>
            {
               new Value(this, parser, parser.EnsureToken(Token.TokenType.Identifier).content)
            };
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.EndOfLine))
            {
                if (parser.IsNextToken(Token.TokenType.Identifier))
                {
                    args.Add(new Value(this, parser, parser.EnsureToken(Token.TokenType.Identifier).content));
                } else if (Value.CanParse(parser))
                {
                    args.Add(new Value(this, parser));
                } else
                {
                    throw new ParserException(string.Format("Improper token for command argument around line {0}.", parser.tokenStream.Peek().line + 1));
                }
            }
        }
    }

    class SceneVariableAssign : Node
    {
        public string variableName;
        public Expression value;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.VariableIdentifier);
        }

        public SceneVariableAssign(Node parent, Parser parser) : base(parent, parser)
        {
            variableName = parser.EnsureToken(Token.TokenType.VariableIdentifier).content;
            parser.EnsureToken(Token.TokenType.Equals);
            value = Expression.Parse(this, parser);
            parser.EnsureToken(Token.TokenType.EndOfLine);
        }
    }

    struct Clause
    {
        public Expression expression;
        public List<SceneStatement> statements;
    }

    class SceneIfStatement : Node
    {
        public Clause mainClause;
        public List<Clause> elseClauses;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("if");
        }

        public SceneIfStatement(Node parent, Parser parser) : base(parent, parser)
        {
            parser.EnsureToken(Token.TokenType.Keyword, "if");
            mainClause = new Clause()
            {
                expression = Expression.Parse(this, parser)
            };
            parser.EnsureToken(Token.TokenType.Colon);

            parser.EnsureToken(Token.TokenType.Indent);
            mainClause.statements = new List<SceneStatement>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                mainClause.statements.Add(new SceneStatement(this, parser));
            }
            parser.EnsureToken(Token.TokenType.Dedent);

            elseClauses = new List<Clause>();
            while (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("else"))
            {
                parser.EnsureToken(Token.TokenType.Keyword, "else");
                if (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("if"))
                {
                    parser.EnsureToken(Token.TokenType.Keyword, "if");
                    Clause next = new Clause()
                    {
                        expression = Expression.Parse(this, parser)
                    };
                    parser.EnsureToken(Token.TokenType.Colon);
                    parser.EnsureToken(Token.TokenType.Indent);
                    next.statements = new List<SceneStatement>();
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        next.statements.Add(new SceneStatement(this, parser));
                    }
                    parser.EnsureToken(Token.TokenType.Dedent);
                    elseClauses.Add(next);
                } else
                {
                    // Final else clause, read it and break out of loop
                    parser.EnsureToken(Token.TokenType.Colon);
                    Clause final = new Clause();
                    parser.EnsureToken(Token.TokenType.Indent);
                    final.statements = new List<SceneStatement>();
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        final.statements.Add(new SceneStatement(this, parser));
                    }
                    parser.EnsureToken(Token.TokenType.Dedent);
                    elseClauses.Add(final);
                    break;
                }
            }
        }
    }

    struct Choice
    {
        public string choiceText;
        public List<SceneStatement> statements;
        public Expression condition;
    }

    class SceneChoiceStatement : Node
    {
        public List<Choice> choices;

        public static bool CanParse(Parser parser)
        {
            return (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("choice"))
                || parser.AreNextTokens(Token.TokenType.String, Token.TokenType.Colon);
        }

        public SceneChoiceStatement(Node parent, Parser parser) : base(parent, parser)
        {
            if (parser.IsNextToken(Token.TokenType.Keyword))
            {
                // Regular choice statement
                parser.EnsureToken(Token.TokenType.Keyword, "choice");
                parser.EnsureToken(Token.TokenType.Colon);

                choices = new List<Choice>();
                parser.EnsureToken(Token.TokenType.Indent);
                while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                {
                    Choice choice = new Choice()
                    {
                        choiceText = parser.EnsureToken(Token.TokenType.String).content,
                        statements = new List<SceneStatement>()
                    };
                    parser.EnsureToken(Token.TokenType.Colon);
                    if (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("if"))
                    {
                        parser.EnsureToken(Token.TokenType.Keyword, "if");
                        choice.condition = Expression.Parse(this, parser);
                    }
                    parser.EnsureToken(Token.TokenType.Indent);
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        choice.statements.Add(new SceneStatement(this, parser));
                    }
                    parser.EnsureToken(Token.TokenType.Dedent);

                    choices.Add(choice);
                }
                parser.EnsureToken(Token.TokenType.Dedent);
            } else
            {
                // Modified choice statement (no keyword and indent, etc.)
                choices = new List<Choice>();

                while (parser.tokenStream.Count > 0 && parser.AreNextTokens(Token.TokenType.String, Token.TokenType.Colon))
                {
                    Choice choice = new Choice()
                    {
                        choiceText = parser.EnsureToken(Token.TokenType.String).content,
                        statements = new List<SceneStatement>()
                    };
                    parser.EnsureToken(Token.TokenType.Colon);
                    if (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("if"))
                    {
                        parser.EnsureToken(Token.TokenType.Keyword, "if");
                        choice.condition = Expression.Parse(this, parser);
                    }
                    parser.EnsureToken(Token.TokenType.Indent);
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        choice.statements.Add(new SceneStatement(this, parser));
                    }
                    parser.EnsureToken(Token.TokenType.Dedent);

                    choices.Add(choice);
                }
            }
        }
    }

    class SceneSpecialName : Node
    {
        public string charName;
        public string dialogue;

        public static bool CanParse(Parser parser)
        {
            return parser.AreNextTokens(Token.TokenType.Identifier, Token.TokenType.Colon, Token.TokenType.String);
        }

        public SceneSpecialName(Node parent, Parser parser) : base(parent, parser)
        {
            charName = parser.EnsureToken(Token.TokenType.Identifier).content;
            parser.EnsureToken(Token.TokenType.Colon);
            dialogue = parser.EnsureToken(Token.TokenType.String).content;
            parser.EnsureToken(Token.TokenType.EndOfLine);
        }
    }

    class SceneStatement : Node
    {
        public enum Type
        {
            Text,
            Command,
            VariableAssign,
            IfStatement,
            ChoiceStatement,
            SpecialName
        }
        public Type type;
        public SceneText text;
        public SceneCommand command;
        public SceneVariableAssign variableAssign;
        public SceneIfStatement ifStatement;
        public SceneChoiceStatement choiceStatement;

        public SceneStatement(Node parent, Parser parser, Type type) : base(parent, parser)
        {
            this.type = type;
        }

        public SceneStatement(Node parent, Parser parser) : base(parent, parser)
        {
            if (SceneText.CanParse(parser))
            {
                type = Type.Text;
                text = new SceneText(this, parser);
            } else if (SceneSpecialName.CanParse(parser))
            {
                type = Type.SpecialName;
                SceneSpecialName sn = new SceneSpecialName(this, parser);
                text = new SceneText(this, parser, sn.dialogue);
                command = new SceneCommand(this, parser, new List<Value>() {
                    new Value(this, parser, "char"),
                    new Value(this, parser, sn.charName)
                });
            } else if (SceneCommand.CanParse(parser))
            {
                type = Type.Command;
                command = new SceneCommand(this, parser);
            } else if (SceneVariableAssign.CanParse(parser))
            {
                type = Type.VariableAssign;
                variableAssign = new SceneVariableAssign(this, parser);
            } else if (SceneIfStatement.CanParse(parser))
            {
                type = Type.IfStatement;
                ifStatement = new SceneIfStatement(this, parser);
            } else if (SceneChoiceStatement.CanParse(parser))
            {
                type = Type.ChoiceStatement;
                choiceStatement = new SceneChoiceStatement(this, parser);
            } else
            {
                throw new ParserException(string.Format("Unable to find any scene statement to parse! Around line {0}.", parser.tokenStream.Peek().line + 1));
            }
        }
    }

    class TextDefinition : Node
    {
        public string key;
        public string value;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Identifier);
        }

        public TextDefinition(Node parent, Parser parser) : base(parent, parser)
        {
            key = parser.EnsureToken(Token.TokenType.Identifier).content;
            parser.EnsureToken(Token.TokenType.Equals);
            value = parser.EnsureToken(Token.TokenType.String).content;
        }
    }

    class DefinitionGroup : Node
    {
        public List<TextDefinition> definitions;
        public string groupName;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("definitions");
        }

        public DefinitionGroup(Node parent, Parser parser) : base(parent, parser)
        {
            parser.EnsureToken(Token.TokenType.Keyword, "definitions");
            groupName = parser.EnsureToken(Token.TokenType.Identifier).content;
            parser.EnsureToken(Token.TokenType.Colon);
            
            parser.EnsureToken(Token.TokenType.Indent);
            definitions = new List<TextDefinition>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                definitions.Add(new TextDefinition(this, parser));
            }
            parser.EnsureToken(Token.TokenType.Dedent);
        }
    }

    class Scene : Node
    {
        public List<SceneStatement> statements;
        public string name;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("scene");
        }

        public Scene(Node parent, Parser parser) : base(parent, parser)
        {
            parser.EnsureToken(Token.TokenType.Keyword, "scene");
            name = parser.EnsureToken(Token.TokenType.Identifier).content;
            parser.EnsureToken(Token.TokenType.Colon);

            parser.EnsureToken(Token.TokenType.Indent);
            statements = new List<SceneStatement>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                SceneStatement s = new SceneStatement(this, parser);
                if (s.type != SceneStatement.Type.SpecialName)
                {
                    statements.Add(s);
                } else
                {
                    s.command.parent = this;
                    statements.Add(new SceneStatement(this, parser, SceneStatement.Type.Command)
                    {
                        command = s.command
                    });
                    s.text.parent = this;
                    statements.Add(new SceneStatement(this, parser, SceneStatement.Type.Text)
                    {
                        text = s.text
                    });
                }
            }
            parser.EnsureToken(Token.TokenType.Dedent);
        }
    }

    class Namespace : Node
    {
        public Block block;
        public string name;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("namespace");
        }

        public Namespace(Node parent, Parser parser) : base(parent, parser)
        {
            parser.EnsureToken(Token.TokenType.Keyword, "namespace");
            name = parser.EnsureToken(Token.TokenType.Identifier).content;
            parser.EnsureToken(Token.TokenType.Colon);

            block = new Block(this, parser);
        }
    }

    class Block : Node
    {
        public List<Statement> statements;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Indent);
        }

        public Block(Node parent, Parser parser) : base(parent, parser)
        {
            if (parent != null)
                parser.EnsureToken(Token.TokenType.Indent);

            statements = new List<Statement>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                statements.Add(new Statement(this, parser));
            }

            if (parent != null)
                parser.EnsureToken(Token.TokenType.Dedent);
        }
    }

    class FunctionCall
    {
        public Function function;
        public List<Expression> parameters;
    }

    class Expression : Node
    {
        public Value value;
        public FunctionCall func;

        public Expression(Node parent, Value value, Parser parser) : base(parent, parser)
        {
            this.value = value;
        }

        public Expression(Node parent, FunctionCall func, Parser parser) : base(parent, parser)
        {
            this.func = func;
        }

        public static Expression Parse(Node parent, Parser parser)
        {
            // Put the tokens into reverse polish notation using the shunting-yard algorithm
            // This part is heavily inspired by YarnSpinner
            Queue<Token> expressionRPN = new Queue<Token>();
            Stack<Token> operatorStack = new Stack<Token>();
            Stack<Token> functionStack = new Stack<Token>();

            Token.TokenType[] accepted =
            {
                Token.TokenType.Number,
                Token.TokenType.VariableIdentifier,
                Token.TokenType.String,
                Token.TokenType.Identifier,
                Token.TokenType.OpenParen,
                Token.TokenType.CloseParen,
                Token.TokenType.Comma,
                Token.TokenType.True,
                Token.TokenType.False,
                Token.TokenType.Undefined,
                Token.TokenType.BinaryOperator,
                Token.TokenType.CompareOperator
            };

            Token last = null;
            while (parser.tokenStream.Count > 0 && parser.IsNextTokenDontRemoveEOL(accepted))
            {
                Token next = parser.EnsureToken(accepted);
                switch (next.type)
                {
                    case Token.TokenType.Number:
                    case Token.TokenType.VariableIdentifier:
                    case Token.TokenType.String:
                    case Token.TokenType.True:
                    case Token.TokenType.False:
                    case Token.TokenType.Undefined:
                        expressionRPN.Enqueue(next);
                        break;
                    case Token.TokenType.Identifier:
                        operatorStack.Push(next);
                        functionStack.Push(next);
                        next = parser.EnsureToken(Token.TokenType.OpenParen);
                        operatorStack.Push(next);
                        break;
                    case Token.TokenType.Comma:
                        try {
                            while (operatorStack.Peek().type != Token.TokenType.OpenParen)
                            {
                                expressionRPN.Enqueue(operatorStack.Pop());
                            }
                        } catch (InvalidOperationException)
                        {
                            throw new ParserException(string.Format("Detected unclosed parentheses in expression. Around line {0}.", next.line + 1));
                        }
                        if (operatorStack.Peek().type != Token.TokenType.OpenParen)
                            throw new ParserException(string.Format("Unknown error parsing function around line {0}.", operatorStack.Peek().line + 1));
                        if (parser.IsNextToken(Token.TokenType.CloseParen, Token.TokenType.Comma))
                            throw new ParserException(string.Format("Expected expression around line {0}.", parser.tokenStream.Peek().line + 1));
                        functionStack.Peek().paramCount++;
                        break;
                    case Token.TokenType.OpenParen:
                        operatorStack.Push(next);
                        break;
                    case Token.TokenType.CloseParen:
                        try
                        {
                            while (operatorStack.Peek().type != Token.TokenType.OpenParen)
                            {
                                expressionRPN.Enqueue(operatorStack.Pop());
                            }
                            operatorStack.Pop();
                        }
                        catch (InvalidOperationException)
                        {
                            throw new ParserException(string.Format("Detected unclosed parentheses in expression. Around line {0}.", next.line + 1));
                        }

                        if (operatorStack.Peek().type == Token.TokenType.Identifier)
                        {
                            if (last.type != Token.TokenType.OpenParen)
                            {
                                functionStack.Peek().paramCount++;
                            }
                            expressionRPN.Enqueue(operatorStack.Pop());
                            functionStack.Pop();
                        }
                        break;
                    default:
                        if (next.type == Token.TokenType.CompareOperator || next.type == Token.TokenType.BinaryOperator || next.type == Token.TokenType.UnaryMinus || next.type == Token.TokenType.UnaryInvert)
                        {
                            if (next.content == "-")
                            {
                                if (last == null || last.type == Token.TokenType.OpenParen || (last.type == Token.TokenType.CompareOperator || last.type == Token.TokenType.BinaryOperator || last.type == Token.TokenType.UnaryMinus))
                                {
                                    next.type = Token.TokenType.UnaryMinus;
                                }
                            } else if (next.content == "!")
                            {
                                if (last == null || last.type == Token.TokenType.OpenParen || (last.type == Token.TokenType.CompareOperator || last.type == Token.TokenType.BinaryOperator || last.type == Token.TokenType.UnaryMinus))
                                {
                                    next.type = Token.TokenType.UnaryInvert;
                                }
                            }

                            while (PrecedenceApplies(next, operatorStack))
                            {
                                expressionRPN.Enqueue(operatorStack.Pop());
                            }
                            operatorStack.Push(next);
                        }
                        break;
                }
                last = next;
            }

            while (operatorStack.Count > 0)
                expressionRPN.Enqueue(operatorStack.Pop());

            if (expressionRPN.Count == 0)
                throw new ParserException(string.Format("Expected expression, but none found. Around line {0}.", parser.tokenStream.Peek().line + 1));

            // Build the tree
            Token first = expressionRPN.Peek();
            Stack<Expression> evaluationStack = new Stack<Expression>();
            while (expressionRPN.Count > 0)
            {
                Token next = expressionRPN.Dequeue();
                if (next.type == Token.TokenType.BinaryOperator || next.type == Token.TokenType.CompareOperator || next.type == Token.TokenType.UnaryMinus || next.type == Token.TokenType.UnaryInvert)
                {
                    Operator.OperatorInfo info = Operator.GetTokenOperator(next);
                    if (evaluationStack.Count < info.args)
                        throw new ParserException(string.Format("Not enough arguments for operator \"{0}\" in expression around line {1}.", next.content, next.line + 1));
                    List<Expression> parameters = new List<Expression>();
                    for (int i = 0; i < info.args; i++)
                        parameters.Add(evaluationStack.Pop());
                    parameters.Reverse();
                    FunctionCall call = new FunctionCall()
                    {
                        function = BuiltinFunctions.Get(next.content),
                        parameters = parameters
                    };
                    Expression expr = new Expression(parent, call, parser);
                    evaluationStack.Push(expr);
                } else if (next.type == Token.TokenType.Identifier)
                {
                    List<Expression> parameters = new List<Expression>();
                    for (int i = 0; i < next.paramCount; i++)
                        parameters.Add(evaluationStack.Pop());
                    parameters.Reverse();
                    FunctionCall call = new FunctionCall()
                    {
                        function = new Function(next.content, -1),
                        parameters = parameters
                    };
                    Expression expr = new Expression(parent, call, parser);
                    evaluationStack.Push(expr);
                } else
                {
                    Value v = new Value(parent, parser, next);
                    Expression expr = new Expression(parent, v, parser);
                    evaluationStack.Push(expr);
                }
            }

            if (evaluationStack.Count != 1)
                throw new ParserException(string.Format("Failed to reduce stack when parsing expression. Around line {0}.", parser.tokenStream.Peek().line + 1));

            return evaluationStack.Pop();
        }

        private static bool PrecedenceApplies(Token t1, Stack<Token> operatorStack)
        {
            if (operatorStack.Count == 0)
            {
                return false;
            }
            if (t1.type != Token.TokenType.BinaryOperator && t1.type != Token.TokenType.CompareOperator && t1.type != Token.TokenType.UnaryMinus && t1.type != Token.TokenType.UnaryInvert)
            {
                throw new ParserException(string.Format("Invalid operator in expression. Around line {0}.", t1.line + 1));
            }
            Token t2 = operatorStack.Peek();

            if (t2.type != Token.TokenType.BinaryOperator && t2.type != Token.TokenType.CompareOperator && t2.type != Token.TokenType.UnaryMinus && t2.type != Token.TokenType.UnaryInvert)
                return false;

            var t1Info = Operator.GetTokenOperator(t1);
            var t2Info = Operator.GetTokenOperator(t2);

            if (t1Info.assoc == Operator.Associativity.Left && t1Info.precedence <= t2Info.precedence)
            {
                return true;
            }
            if (t1Info.assoc == Operator.Associativity.Right && t1Info.precedence < t2Info.precedence)
            {
                return true;
            }
            return false;
        }
    }

    class Operator : Node
    {
        public Token op;

        public enum Associativity
        {
            Left,
            Right
        }

        public struct OperatorInfo
        {
            public Associativity assoc;
            public int precedence;
            public int args;

            public OperatorInfo(Associativity assoc, int precedence, int args)
            {
                this.assoc = assoc;
                this.precedence = precedence;
                this.args = args;
            }
        }

        public static OperatorInfo GetTokenOperator(Token t)
        {
            if (t.type == Token.TokenType.UnaryMinus || t.type == Token.TokenType.UnaryInvert)
                return new OperatorInfo(Associativity.Right, 9, 1);
            switch (t.content)
            {
                case "*":
                case "/":
                case "%":
                    return new OperatorInfo(Associativity.Left, 8, 2);
                case "+":
                case "-":
                    return new OperatorInfo(Associativity.Left, 7, 2);
                case ">":
                case "<":
                case ">=":
                case "<=":
                    return new OperatorInfo(Associativity.Left, 6, 2);
                case "==":
                case "!=":
                    return new OperatorInfo(Associativity.Left, 5, 2);
                case "&&":
                    return new OperatorInfo(Associativity.Left, 4, 2);
                case "||":
                    return new OperatorInfo(Associativity.Left, 3, 2);
                case "^^":
                    return new OperatorInfo(Associativity.Left, 2, 2);
                default:
                    throw new ParserException(string.Format("Invalid operator detected. Around line {0}.", t.line + 1));
            }
        }

        public Operator(Node parent, Parser parser, Token token) : base (parent, parser)
        {
            op = token;
        }
    }

    class Value : Node
    {
        public enum Type
        {
            Double = 0,
            Int32 = 1,
            String = 2,
            Boolean = 3,
            Undefined = 4,
            Variable = 5,
            RawIdentifier = 6 // Special case
        }
        public Type type;
        public double valueDouble;
        public int valueInt32;
        public string valueString;
        public bool valueBoolean;
        public string valueVariable;
        public string valueRawIdentifier;

        public uint stringID; // For use by compiler

        public void FromToken(Token t)
        {
            switch (t.type)
            {
                case Token.TokenType.Number:
                    if (t.content.Contains("."))
                    {
                        type = Type.Double;
                        valueDouble = double.Parse(t.content);
                    } else
                    {
                        type = Type.Int32;
                        valueInt32 = int.Parse(t.content);
                    }
                    break;
                case Token.TokenType.String:
                    type = Type.String;
                    valueString = t.content;
                    break;
                case Token.TokenType.False:
                    type = Type.Boolean;
                    valueBoolean = false;
                    break;
                case Token.TokenType.True:
                    type = Type.Boolean;
                    valueBoolean = true;
                    break;
                case Token.TokenType.Undefined:
                    type = Type.Undefined;
                    break;
                case Token.TokenType.VariableIdentifier:
                    type = Type.Variable;
                    valueVariable = t.content;
                    break;
                default:
                    throw new ParserException(string.Format("Invalid Value token type around line {0}.", t.line + 1));
            }
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != GetType())
                return false;
            Value other = (Value)obj;
            if (other.type != type)
                return false;
            switch (type)
            {
                case Type.Boolean:
                    return valueBoolean == other.valueBoolean;
                case Type.Double:
                    return valueDouble == other.valueDouble;
                case Type.Int32:
                    return valueInt32 == other.valueInt32;
                case Type.RawIdentifier:
                    return valueRawIdentifier == other.valueRawIdentifier;
                case Type.String:
                    return valueString == other.valueString;
                case Type.Undefined:
                    return true;
                case Type.Variable:
                    return valueVariable == other.valueVariable;
            }
            return false;
        }

        public override int GetHashCode()
        {
            switch (type)
            {
                case Type.Boolean:
                    return Tuple.Create(type, valueBoolean).GetHashCode();
                case Type.Double:
                    return Tuple.Create(type, valueDouble).GetHashCode();
                case Type.Int32:
                    return Tuple.Create(type, valueInt32).GetHashCode();
                case Type.RawIdentifier:
                    return Tuple.Create(type, valueRawIdentifier).GetHashCode();
                case Type.String:
                    return Tuple.Create(type, valueString).GetHashCode();
                case Type.Undefined:
                    return type.GetHashCode();
                case Type.Variable:
                    return Tuple.Create(type, valueVariable).GetHashCode();
            }
            return 0;
        }

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Number, Token.TokenType.String, Token.TokenType.VariableIdentifier, Token.TokenType.True, Token.TokenType.False, Token.TokenType.Undefined);
        }

        public Value(Node parent, Parser parser) : base(parent, parser)
        {
            FromToken(parser.EnsureToken(Token.TokenType.Number, Token.TokenType.String, Token.TokenType.VariableIdentifier, Token.TokenType.True, Token.TokenType.False, Token.TokenType.Undefined));
        }

        public Value(Node parent, Parser parser, Token t) : base(parent, parser)
        {
            FromToken(t);
        }

        public Value(Node parent, Parser parser, string rawIdentifier) : base(parent, parser)
        {
            type = Type.RawIdentifier;
            valueRawIdentifier = rawIdentifier;
        }

        public override string ToString()
        {
            switch (type)
            {
                case Type.Boolean:
                    return valueBoolean.ToString();
                case Type.Double:
                    return valueDouble.ToString();
                case Type.Int32:
                    return valueInt32.ToString();
                case Type.RawIdentifier:
                    return valueRawIdentifier.ToString();
                case Type.String:
                    return valueString.ToString();
                case Type.Undefined:
                    return "undefined";
                case Type.Variable:
                    return "$" + valueVariable;
                default:
                    return null;
            }
        }

        public Value ConvertTo(Value.Type type)
        {
            if (this.type == type)
                return this;

            switch (this.type)
            {
                case Type.Double:
                    switch (type)
                    {
                        case Type.Int32:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueInt32 = (int)(this.valueDouble)
                            };
                        case Type.String:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueString = this.valueDouble.ToString()
                            };
                    }
                    break;
                case Type.Int32:
                    switch (type)
                    {
                        case Type.Double:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueDouble = (double)(this.valueInt32)
                            };
                        case Type.String:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueString = this.valueInt32.ToString()
                            };
                    }
                    break;
                case Type.String:
                    switch (type)
                    {
                        case Type.Double:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueDouble = double.Parse(this.valueString)
                            };
                        case Type.Int32:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueInt32 = int.Parse(this.valueString)
                            };
                        case Type.Boolean:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueBoolean = (this.valueString != "")
                            };
                    }
                    break;
                case Type.Boolean:
                    switch (type)
                    {
                        case Type.Double:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueDouble = (valueBoolean ? 1d : 0d)
                            };
                        case Type.Int32:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueInt32 = (valueBoolean ? 1 : 0)
                            };
                        case Type.String:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueString = (valueBoolean ? "true" : "false")
                            };
                    }
                    break;
                case Type.Undefined:
                    switch (type)
                    {
                        case Type.String:
                            return new Value(this.parent, null)
                            {
                                type = type,
                                valueString = "undefined"
                            };
                    }
                    break;
            }

            throw new ParserException(string.Format("Cannot convert type {0} to {1}.", this.type, type));
        }
    }

    class Parser
    {
        public Queue<Token> tokenStream;
        public Block block;

        public Parser(IEnumerable<Token> tokens)
        {
            tokenStream = new Queue<Token>(tokens);
            block = new Block(null, this);
        }

        public Token EnsureToken(Token.TokenType type)
        {
            if (tokenStream.Count == 0)
                throw new ParserException("Unexpected end of code.");
            if (type == Token.TokenType.EndOfLine)
            {
                if (type != tokenStream.Peek().type)
                    throw new ParserException(string.Format("Expected token of type {0}, got {1}. At line {2}.", type, tokenStream.Peek().type, tokenStream.Peek().line + 1));
            } else
            {
                // If not searching for end of lines, remove them before we run into problems
                if (tokenStream.Peek().type == Token.TokenType.EndOfLine)
                    tokenStream.Dequeue();
                if (tokenStream.Count == 0)
                    throw new ParserException("Unexpected end of code.");
                if (type != tokenStream.Peek().type)
                    throw new ParserException(string.Format("Expected token of type {0}, got {1}. At line {2}.", type, tokenStream.Peek().type, tokenStream.Peek().line + 1));
            }
            return tokenStream.Dequeue();
        }

        public Token EnsureToken(params Token.TokenType[] types)
        {
            if (tokenStream.Count == 0)
                throw new ParserException("Unexpected end of code.");
            // If not searching for end of lines, remove them before we run into problems
            if (tokenStream.Peek().type == Token.TokenType.EndOfLine)
                tokenStream.Dequeue();
            if (tokenStream.Count == 0)
                throw new ParserException("Unexpected end of code.");
            if (!types.Contains(tokenStream.Peek().type))
                throw new ParserException(string.Format("Expected token of types {0}, but got {1}. At line {2}.", string.Join(", or ", types.Select(x => x.ToString()).ToArray()), tokenStream.Peek().type, tokenStream.Peek().line + 1));
            return tokenStream.Dequeue();
        }

        public Token EnsureToken(Token.TokenType type, string content)
        {
            // If not searching for end of lines, remove them before we run into problems
            if (tokenStream.Peek().type == Token.TokenType.EndOfLine)
                tokenStream.Dequeue();
            if (tokenStream.Count == 0)
                throw new ParserException("Unexpected end of code.");
            if (type != tokenStream.Peek().type)
                throw new ParserException(string.Format("Expected token of type {0}, got {1}. At line {2}.", type, tokenStream.Peek().type, tokenStream.Peek().line + 1));
            if (content != tokenStream.Peek().content)
                throw new ParserException(string.Format("Expected token of content {0}, got {1}. At line {2}.", content, tokenStream.Peek().type, tokenStream.Peek().line + 1));
            return tokenStream.Dequeue();
        }

        public Token EnsureToken(string content)
        {
            // If not searching for end of lines, remove them before we run into problems
            if (tokenStream.Peek().type == Token.TokenType.EndOfLine)
                tokenStream.Dequeue();
            if (tokenStream.Count == 0)
                throw new ParserException("Unexpected end of code.");
            if (content != tokenStream.Peek().content)
                throw new ParserException(string.Format("Expected token of content {0}, got {1}. At line {2}.", content, tokenStream.Peek().type, tokenStream.Peek().line + 1));
            return tokenStream.Dequeue();
        }

        public bool IsNextToken(params Token.TokenType[] types)
        {
            if (!types.Contains(Token.TokenType.EndOfLine))
            {
                // If not searching for end of lines, remove them before we run into problems
                if (tokenStream.Peek().type == Token.TokenType.EndOfLine)
                    tokenStream.Dequeue();
            }
            if (tokenStream.Count == 0)
                return false;
            return types.Contains(tokenStream.Peek().type);
        }

        public bool AreNextTokens(params Token.TokenType[] tokens)
        {
            if (tokens.Length >= 1 && tokens[0] != Token.TokenType.EndOfLine)
            {
                // If not searching for end of lines, remove them before we run into problems
                if (tokenStream.Peek().type == Token.TokenType.EndOfLine)
                    tokenStream.Dequeue();
            }
            if (tokenStream.Count < tokens.Length)
                return false;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokenStream.Skip(i).First().type != tokens[i])
                    return false;
            }
            return true;
        }

        public bool IsTokenAfterNext(Token.TokenType t)
        {

            if (t != Token.TokenType.EndOfLine)
            {
                // If not searching for end of lines, remove them before we run into problems
                if (tokenStream.Peek().type == Token.TokenType.EndOfLine)
                    tokenStream.Dequeue();
            }
            if (tokenStream.Count < 1)
                return false;
            return t == tokenStream.Skip(1).First().type;
        }

        public bool IsNextTokenDontRemoveEOL(params Token.TokenType[] types)
        {
            return types.Contains(tokenStream.Peek().type);
        }

        public bool IsNextToken(params string[] contents)
        {
            return contents.Contains(tokenStream.Peek().content);
        }
    }

    [Serializable]
    internal class ParserException : Exception
    {
        public ParserException()
        {
        }

        public ParserException(string message) : base(message)
        {
        }

        public ParserException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ParserException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
