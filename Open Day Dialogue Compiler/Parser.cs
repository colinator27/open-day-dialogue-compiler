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
                Parser.ParserError.ReportSync(parser, "Unable to find any statement to parse!", parser.tokenStream.Peek().line + 1);
                return;
            }
        }
    }

    class SceneText : Node
    {
        public string text;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.String) && !parser.IsTokenAfterNext(Token.TokenType.Colon);
        }

        public SceneText(Node parent, Parser parser, string content) : base(parent, parser)
        {
            text = content;
        }

        public SceneText(Node parent, Parser parser) : base(parent, parser)
        {
            Token t = parser.EnsureToken(Token.TokenType.String);
            if (t == null) return;
            text = t.content;

            if (parser.EnsureToken(Token.TokenType.EndOfLine) == null) return;
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
                    Token t = parser.EnsureToken(Token.TokenType.Identifier);
                    if (t == null) return;
                    args.Add(new Value(this, parser, t.content));
                } else if (Value.CanParse(parser))
                {
                    args.Add(new Value(this, parser));
                } else
                {
                    Parser.ParserError.ReportSync(parser, "Improper token for command argument.", parser.tokenStream.Peek().line + 1);
                    return;
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
            Token t = parser.EnsureToken(Token.TokenType.VariableIdentifier);
            if (t == null) return;
            variableName = t.content;
            if (parser.IsNextToken(Token.TokenType.Equals))
            {
                if (parser.EnsureToken(Token.TokenType.Equals) == null) return;
                value = Expression.Parse(this, parser);
                if (value == null) return;
                if (parser.EnsureToken(Token.TokenType.EndOfLine) == null) return;
            } else
            {
                t = parser.EnsureToken(Token.TokenType.SpecialAssignmentOperator);
                if (t == null) return;
                string op = t.content;

                // Do work, depending on operator
                Expression other = null;
                string builtinFunc = "";
                switch (op)
                {
                    case "+=":
                        other = Expression.Parse(this, parser);
                        if (other == null) return;
                        builtinFunc = "+";
                        break;
                    case "-=":
                        other = Expression.Parse(this, parser);
                        if (other == null) return;
                        builtinFunc = "-";
                        break;
                    case "*=":
                        other = Expression.Parse(this, parser);
                        if (other == null) return;
                        builtinFunc = "*";
                        break;
                    case "/=":
                        other = Expression.Parse(this, parser);
                        if (other == null) return;
                        builtinFunc = "/";
                        break;
                    case "%=":
                        other = Expression.Parse(this, parser);
                        if (other == null) return;
                        builtinFunc = "%";
                        break;
                    case "++":
                        other = new Expression(this, new Value(this, parser, new Token()
                        {
                            type = Token.TokenType.Number,
                            content = "1"
                        }), parser);
                        builtinFunc = "+";
                        break;
                    case "--":
                        other = new Expression(this, new Value(this, parser, new Token()
                        {
                            type = Token.TokenType.Number,
                            content = "1"
                        }), parser);
                        builtinFunc = "-";
                        break;
                }

                value = new Expression(this, new FunctionCall()
                {
                    function = BuiltinFunctions.Get(builtinFunc),
                    parameters = new List<Expression>()
                    {
                        new Expression(this, new Value(this, parser, new Token()
                        {
                            type = Token.TokenType.VariableIdentifier,
                            content = variableName
                        }), parser),
                        other
                    }
                }, parser);

                if (parser.EnsureToken(Token.TokenType.EndOfLine) == null) return;
            }
        }
    }

    class Clause : Node
    {
        public Expression expression;
        public List<SceneStatement> statements;

        public Clause(Node parent) : base(parent, null)
        {

        }
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
            if (parser.EnsureToken(Token.TokenType.Keyword, "if") == null) return;
            mainClause = new Clause(this);
            mainClause.expression = Expression.Parse(mainClause, parser);
            if (mainClause.expression == null) return;
            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

            if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
            mainClause.statements = new List<SceneStatement>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                mainClause.statements.Add(new SceneStatement(this, parser));
            }
            if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;

            elseClauses = new List<Clause>();
            while (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("else"))
            {
                if (parser.EnsureToken(Token.TokenType.Keyword, "else") == null) return;
                if (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("if"))
                {
                    if (parser.EnsureToken(Token.TokenType.Keyword, "if") == null) return;
                    Clause next = new Clause(this);
                    next.expression = Expression.Parse(next, parser);
                    if (next.expression == null) return;
                    if (parser.EnsureToken(Token.TokenType.Colon) == null) return;
                    if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
                    next.statements = new List<SceneStatement>();
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        next.statements.Add(new SceneStatement(this, parser));
                    }
                    if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;
                    elseClauses.Add(next);
                } else
                {
                    // Final else clause, read it and break out of loop
                    if (parser.EnsureToken(Token.TokenType.Colon) == null) return;
                    Clause final = new Clause(this);
                    if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
                    final.statements = new List<SceneStatement>();
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        final.statements.Add(new SceneStatement(this, parser));
                    }
                    if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;
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
                if (parser.EnsureToken(Token.TokenType.Keyword, "choice") == null) return;
                if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

                choices = new List<Choice>();
                if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
                while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                {
                    Token t = parser.EnsureToken(Token.TokenType.String);
                    if (t == null) return;
                    Choice choice = new Choice()
                    {
                        choiceText = t.content,
                        statements = new List<SceneStatement>()
                    };

                    if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

                    if (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("if"))
                    {
                        if (parser.EnsureToken(Token.TokenType.Keyword, "if") == null) return;
                        choice.condition = Expression.Parse(this, parser);
                        if (choice.condition == null) return;
                    }
                    if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        choice.statements.Add(new SceneStatement(this, parser));
                    }
                    if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;

                    choices.Add(choice);
                }
                if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;
            } else
            {
                // Modified choice statement (no keyword and indent, etc.)
                choices = new List<Choice>();

                while (parser.tokenStream.Count > 0 && parser.AreNextTokens(Token.TokenType.String, Token.TokenType.Colon))
                {
                    Token t = parser.EnsureToken(Token.TokenType.String);
                    if (t == null) return;
                    Choice choice = new Choice()
                    {
                        choiceText = t.content,
                        statements = new List<SceneStatement>()
                    };
                    if (parser.EnsureToken(Token.TokenType.Colon) == null) return;
                    if (parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("if"))
                    {
                        if (parser.EnsureToken(Token.TokenType.Keyword, "if") == null) return;
                        choice.condition = Expression.Parse(this, parser);
                        if (choice.condition == null) return;
                    }
                    if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
                    while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                    {
                        choice.statements.Add(new SceneStatement(this, parser));
                    }
                    if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;

                    choices.Add(choice);
                }
            }
        }
    }

    class SceneLabel : Node
    {
        public string name;

        public static bool CanParse(Parser parser)
        {
            return parser.AreNextTokens(Token.TokenType.Identifier, Token.TokenType.Colon, Token.TokenType.EndOfLine);
        }

        public SceneLabel(Node parent, Parser parser) : base(parent, parser)
        {
            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            name = t.content;

            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;
            if (parser.EnsureToken(Token.TokenType.EndOfLine) == null) return;
        }
    }

    class SceneJump : Node
    {
        public string labelName;

        public static bool CanParse(Parser parser)
        {
            return parser.AreNextTokens(Token.TokenType.Colon, Token.TokenType.Identifier, Token.TokenType.EndOfLine);
        }

        public SceneJump(Node parent, Parser parser) : base(parent, parser)
        {
            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            labelName = t.content;

            if (parser.EnsureToken(Token.TokenType.EndOfLine) == null) return;
        }
    }

    class SceneWhileLoop : Node
    {
        public Expression condition;
        public List<SceneStatement> statements;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Keyword) && parser.IsNextToken("while");
        }

        public SceneWhileLoop(Node parent, Parser parser) : base(parent, parser)
        {
            if (parser.EnsureToken(Token.TokenType.Keyword, "while") == null) return;
            condition = Expression.Parse(this, parser);
            if (condition == null) return;
            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

            if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
            statements = new List<SceneStatement>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                statements.Add(new SceneStatement(this, parser));
            }
            if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;
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
            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            charName = t.content;

            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

            t = parser.EnsureToken(Token.TokenType.String);
            if (t == null) return;
            dialogue = t.content;

            if (parser.EnsureToken(Token.TokenType.EndOfLine) == null) return;
        }
    }

    class SceneSpecialCommand : Node
    {
        public string commandName;
        public string operand;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.CompareOperator);
        }

        public SceneSpecialCommand(Node parent, Parser parser) : base(parent, parser)
        {
            Token t = parser.EnsureToken(Token.TokenType.CompareOperator);
            if (t == null) return;
            string op = t.content;
            if (parser.IsNextToken(Token.TokenType.Identifier))
            {
                t = parser.EnsureToken(Token.TokenType.Identifier);
                if (t == null) return;
                operand = t.content;
            }
            switch (op)
            {
                case ">":
                    commandName = "goto";
                    break;
                case "<":
                    commandName = "exit";
                    break;
                default:
                    Parser.ParserError.ReportSync(parser, string.Format("Unsupported special command for operator \"{0}\".", op), parser.tokenStream.Peek().line + 1);
                    commandName = "";
                    break;
            }
        }
    }

    class SceneControlFlow : Node
    {
        public string content;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.Keyword) && (parser.IsNextToken("continue") || parser.IsNextToken("break"));
        }

        public SceneControlFlow(Node parent, Parser parser) : base(parent, parser)
        {
            Token t = parser.EnsureToken(Token.TokenType.Keyword);
            if (t == null) return;
            content = t.content;
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
            Label,
            Jump,
            SpecialName,
            WhileLoop,
            ControlFlow
        }
        public Type type;
        public SceneText text;
        public SceneCommand command;
        public SceneVariableAssign variableAssign;
        public SceneIfStatement ifStatement;
        public SceneChoiceStatement choiceStatement;
        public SceneLabel label;
        public SceneJump jump;
        public SceneWhileLoop whileLoop;
        public SceneControlFlow controlFlow;

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
            } else if (SceneLabel.CanParse(parser))
            {
                type = Type.Label;
                label = new SceneLabel(this, parser);
            } else if (SceneSpecialName.CanParse(parser))
            {
                type = Type.SpecialName;
                SceneSpecialName sn = new SceneSpecialName(this, parser);
                text = new SceneText(this, parser, sn.dialogue);
                command = new SceneCommand(this, parser, new List<Value>()
                {
                    new Value(this, parser, "char"),
                    new Value(this, parser, sn.charName)
                });
            } else if (SceneJump.CanParse(parser))
            {
                type = Type.Jump;
                jump = new SceneJump(this, parser);
            } else if (SceneSpecialCommand.CanParse(parser))
            {
                type = Type.Command;
                SceneSpecialCommand sc = new SceneSpecialCommand(this, parser);

                List<Value> values = new List<Value>()
                {
                    new Value(this, parser, sc.commandName)
                };
                if (sc.operand != null)
                    values.Add(new Value(this, parser, sc.operand));

                command = new SceneCommand(this, parser, values);
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
            } else if (SceneWhileLoop.CanParse(parser))
            {
                type = Type.WhileLoop;
                whileLoop = new SceneWhileLoop(this, parser);
            } else if (SceneControlFlow.CanParse(parser))
            {
                type = Type.ControlFlow;
                controlFlow = new SceneControlFlow(this, parser);
            } else
            {
                Parser.ParserError.ReportSync(parser, "Unable to find any scene statement to parse!", parser.tokenStream.Peek().line + 1);
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
            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            key = t.content;

            if (parser.EnsureToken(Token.TokenType.Equals) == null) return;

            t = parser.EnsureToken(Token.TokenType.String);
            if (t == null) return;
            value = t.content;
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
            if (parser.EnsureToken(Token.TokenType.Keyword, "definitions") == null) return;

            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            groupName = t.content;

            parser.currentItem = groupName;

            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

            if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
            definitions = new List<TextDefinition>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                definitions.Add(new TextDefinition(this, parser));
            }
            if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;

            parser.currentItem = "";
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
            if (parser.EnsureToken(Token.TokenType.Keyword, "scene") == null) return;

            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            name = t.content;

            parser.currentItem = name;

            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

            if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
            statements = new List<SceneStatement>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                statements.Add(new SceneStatement(this, parser));
            }
            if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;

            parser.currentItem = "";
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
            if (parser.EnsureToken(Token.TokenType.Keyword, "namespace") == null) return;

            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            name = t.content;

            if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

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
            {
                if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
            }

            statements = new List<Statement>();
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
            {
                statements.Add(new Statement(this, parser));
            }

            if (parent != null)
            {
                if (parser.EnsureToken(Token.TokenType.Dedent) == null) return;
            }
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

        public Expression(Node parent) : base(parent, null)
        {
            
        }

        /// <summary>
        /// Generates an expression tree from the current Parser, with its token stream
        /// </summary>
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
                if (next == null) return null;
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
                            Parser.ParserError.ReportSync(parser, "Unclosed parentheses in expression.", next.line);
                            return null;
                        }
                        if (operatorStack.Peek().type != Token.TokenType.OpenParen)
                        {
                            Parser.ParserError.ReportSync(parser, "Unknown error parsing function.", operatorStack.Peek().line + 1);
                            return null;
                        }
                        if (parser.IsNextToken(Token.TokenType.CloseParen, Token.TokenType.Comma))
                        {
                            Parser.ParserError.ReportSync(parser, "Expected expression.", parser.tokenStream.Peek().line + 1);
                            return null;
                        }
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
                            Parser.ParserError.ReportSync(parser, "Unclosed parentheses in expression.", next.line);
                            return null;
                        }

                        if (operatorStack.Count < 1)
                        {
                            Parser.ParserError.ReportSync(parser, "Error parsing expression.", next.line + 1);
                            return null;
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
            {
                Parser.ParserError.ReportSync(parser, "Expected expression, but none found.", parser.tokenStream.Peek().line + 1);
                return null;
            }

            // Build the tree
            Token first = expressionRPN.Peek();
            Stack<Expression> evaluationStack = new Stack<Expression>();
            while (expressionRPN.Count > 0)
            {
                Token next = expressionRPN.Dequeue();
                if (next.type == Token.TokenType.BinaryOperator || next.type == Token.TokenType.CompareOperator || next.type == Token.TokenType.UnaryMinus || next.type == Token.TokenType.UnaryInvert)
                {
                    Operator.OperatorInfo info = Operator.GetTokenOperator(next);
                    if (info == null) return null;
                    if (evaluationStack.Count < info.args)
                    {
                        Parser.ParserError.ReportSync(parser, string.Format("Not enough arguments for operator \"{0}\" in expression.", next.content), next.line + 1);
                        return null;
                    }
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
            {
                Parser.ParserError.ReportSync(parser, "Failed to reduce stack when parsing expression.", parser.tokenStream.Peek().line + 1);
                return null;
            }

            return evaluationStack.Pop();
        }

        /// <summary>
        /// Checks if precendence applies when parsing expressions.
        /// </summary>
        private static bool PrecedenceApplies(Token t1, Stack<Token> operatorStack)
        {
            if (operatorStack.Count == 0)
            {
                return false;
            }
            if (t1.type != Token.TokenType.BinaryOperator && t1.type != Token.TokenType.CompareOperator && t1.type != Token.TokenType.UnaryMinus && t1.type != Token.TokenType.UnaryInvert)
            {
                Parser.ParserError.Report("Invalid operator in expression.", t1.line + 1);
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

        public class OperatorInfo
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

        /// <summary>
        /// Gets operator information from a token.
        /// </summary>
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
                    Parser.ParserError.Report("Invalid operator detected", t.line + 1);
                    return null;
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

        /// <summary>
        /// Initializes fields with a token.
        /// </summary>
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
                    throw new FatalErrorException(string.Format("Invalid Value token type around line {0}.", t.line + 1));
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

        public Value(Node parent, Parser parser, int int32) : base(parent, parser)
        {
            type = Type.Int32;
            valueInt32 = int32;
        }

        /// <summary>
        /// Gets a string readable version of the value.
        /// </summary>
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

        /// <summary>
        /// Converts a Value to another type.
        /// </summary>
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

            throw new FatalErrorException(string.Format("Cannot convert type {0} to {1}.", this.type, type));
        }
    }

    class Parser
    {
        public Queue<Token> tokenStream;
        public Block block;
        public string currentItem;

        /// <summary>
        /// Initializes a new Parser, which will automatically begin, synchronously.
        /// </summary>
        public Parser(IEnumerable<Token> tokens)
        {
            tokenStream = new Queue<Token>(tokens);
            block = new Block(null, this);
            currentItem = "";
        }

        /// <summary>
        /// Ensures the next token has a certain type, and consumes it.
        /// </summary>
        /// <returns>The token being consumed, or null.</returns>
        public Token EnsureToken(Token.TokenType type)
        {
            if (tokenStream.Count == 0)
            {
                ParserError.Report("Unexpected end of code.", -1);
                return null;
            }
            if (type == Token.TokenType.EndOfLine)
            {
                if (type != tokenStream.Peek().type)
                {
                    ParserError.ReportSync(this, string.Format("Expected token of type {0}, got {1}.", type, tokenStream.Peek().type), tokenStream.Peek().line + 1);
                    return null;
                }
            } else
            {
                // If not searching for end of lines, remove them before we run into problems
                while (tokenStream.Count > 0 && tokenStream.Peek().type == Token.TokenType.EndOfLine)
                    tokenStream.Dequeue();
                if (tokenStream.Count == 0)
                {
                    ParserError.Report("Unexpected end of code.", -1);
                    return null;
                }
                if (type != tokenStream.Peek().type)
                {
                    ParserError.ReportSync(this, string.Format("Expected token of type {0}, got {1}.", type, tokenStream.Peek().type), tokenStream.Peek().line + 1);
                    return null;
                }
            }
            return tokenStream.Dequeue();
        }

        /// <summary>
        /// Ensures the next token is one of the provided types, and consumes it.
        /// </summary>
        /// <returns>The token being consumed, or null.</returns>
        public Token EnsureToken(params Token.TokenType[] types)
        {
            if (tokenStream.Count == 0)
            {
                ParserError.Report("Unexpected end of code.", -1);
                return null;
            }
            // If not searching for end of lines, remove them before we run into problems
            while (tokenStream.Count > 0 && tokenStream.Peek().type == Token.TokenType.EndOfLine)
                tokenStream.Dequeue();
            if (tokenStream.Count == 0)
            {
                ParserError.Report("Unexpected end of code.", -1);
                return null;
            }
            if (!types.Contains(tokenStream.Peek().type))
            {
                ParserError.ReportSync(this, string.Format("Expected token of types {0}, but got {1}.", string.Join(", or ", types.Select(x => x.ToString()).ToArray()), tokenStream.Peek().type), tokenStream.Peek().line + 1);
                return null;
            }
            return tokenStream.Dequeue();
        }

        /// <summary>
        /// Ensures the next token has a type and string content, and consumes it.
        /// </summary>
        /// <returns>The token being consumed, or null.</returns>
        public Token EnsureToken(Token.TokenType type, string content)
        {
            // If not searching for end of lines, remove them before we run into problems
            while (tokenStream.Count > 0 && tokenStream.Peek().type == Token.TokenType.EndOfLine)
                tokenStream.Dequeue();
            if (tokenStream.Count == 0)
            {
                ParserError.Report("Unexpected end of code.", -1);
                return null;
            }
            if (type != tokenStream.Peek().type)
            {
                ParserError.ReportSync(this, string.Format("Expected token of type {0}, got {1}.", type, tokenStream.Peek().type), tokenStream.Peek().line + 1);
                return null;
            }
            if (content != tokenStream.Peek().content)
            {
                ParserError.ReportSync(this, string.Format("Expected token of content \"{0}\", got \"{1}\".", content, tokenStream.Peek().content), tokenStream.Peek().line + 1);
                return null;
            }
            return tokenStream.Dequeue();
        }

        /// <summary>
        /// Ensures the next token has a string content, and consumes it.
        /// </summary>
        /// <returns>The token being consumed, or null.</returns>
        public Token EnsureToken(string content)
        {
            // If not searching for end of lines, remove them before we run into problems
            while (tokenStream.Count > 0 && tokenStream.Peek().type == Token.TokenType.EndOfLine)
                tokenStream.Dequeue();
            if (tokenStream.Count == 0)
            {
                ParserError.Report("Unexpected end of code.", -1);
                return null;
            }
            if (content != tokenStream.Peek().content)
            {
                ParserError.ReportSync(this, string.Format("Expected token of content \"{0}\", got \"{1}\".", content, tokenStream.Peek().content), tokenStream.Peek().line + 1);
                return null;
            }
            return tokenStream.Dequeue();
        }

        /// <summary>
        /// Checks if the next token is one of the types provided.
        /// </summary>
        /// <returns>True or false.</returns>
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

        /// <summary>
        /// Checks if the next tokens match the array of tokens provided, in order.
        /// </summary>
        /// <returns>True or false.</returns>
        public bool AreNextTokens(params Token.TokenType[] tokens)
        {
            if (tokenStream.Count < tokens.Length)
                return false;
            int j = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                Token t = tokenStream.Skip(j).First();
                if (t.type == Token.TokenType.EndOfLine && tokens[i] != Token.TokenType.EndOfLine)
                {
                    while (j < tokenStream.Count && t.type == Token.TokenType.EndOfLine)
                    {
                        j++;
                        t = tokenStream.Skip(j).First();
                    }
                }
                if (t.type != tokens[i])
                    return false;
                j++;
            }
            return true;
        }

        /// <summary>
        /// Checks if the next token after the next is the type provided.
        /// </summary>
        /// <returns>True or false.</returns>
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

        /// <summary>
        /// Checks if the next token is one of the types provided, and doesn't remove end of line tokens.
        /// </summary>
        /// <returns>True or false.</returns>
        public bool IsNextTokenDontRemoveEOL(params Token.TokenType[] types)
        {
            return types.Contains(tokenStream.Peek().type);
        }

        /// <summary>
        /// Checks if the next token is one of the strings provided.
        /// </summary>
        /// <returns>True or false.</returns>
        public bool IsNextToken(params string[] contents)
        {
            return contents.Contains(tokenStream.Peek().content);
        }
        
        /// <summary>
        /// Re-synchronizes the parser when a bad error is detected, so it can continue to find any other errors.
        /// </summary>
        public void Synchronize()
        {
            while (tokenStream.Count > 0)
            {
                if (IsNextTokenDontRemoveEOL(Token.TokenType.EndOfLine, Token.TokenType.Keyword))
                {
                    return;
                } else
                {
                    tokenStream.Dequeue();
                }
            }
        }

        internal static class ParserError
        {
            /// <summary>
            /// Reports an error in the parser.
            /// </summary>
            public static void Report(string message, int line)
            {
                string l = (line != -1) ? (line.ToString()) : "?";
                Errors.Report(message, l, CodeError.Severity.ErrorDeadly, "Parser");
            }

            /// <summary>
            /// Reports an error in the parser, and synchronizes.
            /// </summary>
            public static void ReportSync(Parser p, string message, int line)
            {
                string l = (line != -1) ? (line.ToString()) : "?";
                Errors.Report(message, l, CodeError.Severity.ErrorDeadly, "Parser", p.currentItem);
                p.Synchronize();
            }

            /// <summary>
            /// Reports a warning in the parser.
            /// </summary>
            public static void Warn(string message, int line)
            {
                Errors.Report(message, line.ToString(), CodeError.Severity.Warning, "Parser");
            }
        }
    }

    [Serializable]
    internal class FatalErrorException : Exception
    {
        public FatalErrorException()
        {
        }

        public FatalErrorException(string message) : base(message)
        {
        }

        public FatalErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected FatalErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
