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
        public int? sceneLine;

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

            sceneLine = t.line + 1;

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
            Token t = parser.EnsureToken(Token.TokenType.Identifier);
            if (t == null) return;
            args = new List<Value>
            {
               new Value(this, parser, t.content)
            };
            sceneLine = t.line + 1;
            while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.EndOfLine))
            {
                if (parser.IsNextToken(Token.TokenType.Identifier))
                {
                    t = parser.EnsureToken(Token.TokenType.Identifier);
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
        public Expression arrayIndex;

        public static bool CanParse(Parser parser)
        {
            return parser.IsNextToken(Token.TokenType.VariableIdentifier);
        }

        public SceneVariableAssign(Node parent, Parser parser) : base(parent, parser)
        {
            Token t = parser.EnsureToken(Token.TokenType.VariableIdentifier);
            if (t == null) return;
            variableName = t.content;
            sceneLine = t.line + 1;

            if (parser.IsNextToken(Token.TokenType.OpenBrack))
            {
                if (parser.EnsureToken(Token.TokenType.OpenBrack) == null) return;
                arrayIndex = Expression.Parse(this, parser);
                if (arrayIndex == null) return;
                if (parser.EnsureToken(Token.TokenType.CloseBrack) == null) return;
            }

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

                if (arrayIndex == null)
                {
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
                } else
                {
                    Expression e = new Expression(this, new Value(this, parser, new Token()
                    {
                        type = Token.TokenType.VariableIdentifier,
                        content = variableName
                    }), parser);
                    e.arrayIndex = arrayIndex;
                    value = new Expression(this, new FunctionCall()
                    {
                        function = BuiltinFunctions.Get(builtinFunc),
                        parameters = new List<Expression>()
                        {
                            e,
                            other
                        }
                    }, parser);
                }

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
            Token t = parser.EnsureToken(Token.TokenType.Keyword, "if");
            if (t == null) return;
            sceneLine = t.line + 1;
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
                Token t = parser.EnsureToken(Token.TokenType.Keyword, "choice");
                if (t == null) return;
                sceneLine = t.line + 1;
                if (parser.EnsureToken(Token.TokenType.Colon) == null) return;

                choices = new List<Choice>();
                if (parser.EnsureToken(Token.TokenType.Indent) == null) return;
                while (parser.tokenStream.Count > 0 && !parser.IsNextToken(Token.TokenType.Dedent))
                {
                    t = parser.EnsureToken(Token.TokenType.String);
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

                bool first = true;
                while (parser.tokenStream.Count > 0 && parser.AreNextTokens(Token.TokenType.String, Token.TokenType.Colon))
                {
                    Token t = parser.EnsureToken(Token.TokenType.String);
                    if (t == null) return;
                    if (first)
                    {
                        sceneLine = t.line + 1;
                        first = false;
                    }
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
            sceneLine = t.line + 1;

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
            sceneLine = t.line + 1;

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
            Token t = parser.EnsureToken(Token.TokenType.Keyword, "while");
            if (t == null) return;
            sceneLine = t.line + 1;
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
            sceneLine = t.line + 1;

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
            sceneLine = t.line + 1;
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
            sceneLine = t.line + 1;
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
                text.sceneLine = sn.sceneLine;
                command = new SceneCommand(this, parser, new List<Value>()
                {
                    new Value(this, parser, "char"),
                    new Value(this, parser, sn.charName)
                });
                command.sceneLine = sn.sceneLine;
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
        public Value value;

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

            value = new Value(parent, parser);
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
        public Expression arrayIndex;
        public FunctionCall func;
        public List<Expression> arrayValues;

        public Expression(Node parent, Value value, Parser parser) : base(parent, parser)
        {
            this.value = value;
        }

        public Expression(Node parent, FunctionCall func, Parser parser) : base(parent, parser)
        {
            this.func = func;
        }

        public Expression(Node parent, List<Expression> arrayValues, Parser parser) : base(parent, parser)
        {
            this.arrayValues = arrayValues;
        }

        public Expression(Node parent) : base(parent, null)
        {
            
        }

        /// <summary>
        /// Generates an expression tree from the current Parser, with its token stream
        /// </summary>
        public static Expression Parse(Node parent, Parser parser)
        {
            return UnaryOperation(parent, parser);
        }
        
        private static Expression UnaryOperation(Node parent, Parser p)
        {
            if ((p.IsNextTokenDontRemoveEOL(Token.TokenType.BinaryOperator) && p.IsNextToken("-")) || 
                (p.IsNextTokenDontRemoveEOL(Token.TokenType.CompareOperator) && p.IsNextToken("!")))
            {
                Token t = p.tokenStream.Dequeue();
                if (t.content == "-")
                    t.type = Token.TokenType.UnaryMinus;
                else
                    t.type = Token.TokenType.UnaryInvert;

                Expression e = new Expression(parent);
                Expression right = Parse(e, p);
                if (right == null) return null;
                e.func = new FunctionCall()
                {
                    function = (t.content == "!" ? BuiltinFunctions.Get(t.content) : new Function("-", 1)),
                    parameters = new List<Expression>() { right }
                };

                return e;
            }

            return Array(parent, p);
        }

        private static Expression Array(Node parent, Parser p)
        {
            if (p.IsNextTokenDontRemoveEOL(Token.TokenType.OpenBrack))
            {
                if (p.EnsureToken(Token.TokenType.OpenBrack) == null) return null;
                Expression e = new Expression(parent, new List<Expression>(), p);
                bool first = true;
                while (!p.IsNextTokenDontRemoveEOL(Token.TokenType.CloseBrack))
                {
                    if (!first)
                    {
                        if (p.EnsureToken(Token.TokenType.Comma) == null) return null;
                    }
                    first = false;

                    Expression val = Parse(e, p);
                    if (val == null) return null;
                    e.arrayValues.Add(val);
                }
                if (p.EnsureToken(Token.TokenType.CloseBrack) == null) return null;

                return e;
            }

            return Group(parent, p);
        }

        private static Expression Group(Node parent, Parser p)
        {
            if (p.IsNextTokenDontRemoveEOL(Token.TokenType.OpenParen))
            {
                if (p.EnsureToken(Token.TokenType.OpenParen) == null) return null;
                Expression e = Parse(parent, p);
                if (p.EnsureToken(Token.TokenType.CloseParen) == null) return null;
                return e;
            }

            return Compare(parent, p);
        }

        private static Expression Compare(Node parent, Parser p)
        {
            Expression e = BinaryOperation(parent, p);

            if (p.IsNextTokenDontRemoveEOL(Token.TokenType.CompareOperator))
            {
                Expression left = e;
                if (left == null) return null;
                Token op = p.EnsureToken(Token.TokenType.CompareOperator);
                if (op == null) return null;
                e = new Expression(parent);
                Expression right = Parse(e, p);
                if (right == null) return null;
                e.func = new FunctionCall()
                {
                    function = BuiltinFunctions.Get(op.content),
                    parameters = new List<Expression>() { left, right }
                };
            }

            return e;
        }
        
        private static Expression BinaryOperation(Node parent, Parser p)
        {
            Expression e = Literal(parent, p);

            if (p.IsNextTokenDontRemoveEOL(Token.TokenType.BinaryOperator))
            {
                Expression left = e;
                if (left == null) return null;
                Token op = p.EnsureToken(Token.TokenType.BinaryOperator);
                if (op == null) return null;
                e = new Expression(parent);
                Expression right = Parse(e, p);
                if (right == null) return null;
                e.func = new FunctionCall()
                {
                    function = BuiltinFunctions.Get(op.content),
                    parameters = new List<Expression>() { left, right }
                };
            }

            return e;
        }

        private static Token.TokenType[] literals =
        {
            Token.TokenType.True,
            Token.TokenType.False,
            Token.TokenType.Undefined,
            Token.TokenType.Number,
            Token.TokenType.VariableIdentifier,
            Token.TokenType.String
        };

        private static Expression Literal(Node parent, Parser p)
        {
            if (p.IsNextTokenDontRemoveEOL(literals))
            {
                Token t = p.EnsureToken(literals);
                if (t == null) return null;
                if (t.type == Token.TokenType.VariableIdentifier)
                {
                    if (p.IsNextToken(Token.TokenType.OpenBrack))
                    {
                        if (p.EnsureToken(Token.TokenType.OpenBrack) == null) return null;
                        Expression e = new Expression(parent, new Value(parent, p, t), p);
                        e.arrayIndex = Parse(e, p);
                        if (e.arrayIndex == null) return null;
                        if (p.EnsureToken(Token.TokenType.CloseBrack) == null) return null;
                        return e;
                    }
                }
                return new Expression(parent, new Value(parent, p, t), p);
            } else if (p.IsNextTokenDontRemoveEOL(Token.TokenType.Identifier))
            {
                Token t = p.EnsureToken(Token.TokenType.Identifier);
                if (t == null) return null;
                if (p.EnsureToken(Token.TokenType.OpenParen) == null) return null;
                Expression e = new Expression(parent);
                e.func = new FunctionCall()
                {
                    function = new Function(t.content, -1),
                    parameters = new List<Expression>()
                };
                bool first = true;
                while (!p.IsNextTokenDontRemoveEOL(Token.TokenType.CloseParen))
                {
                    if (!first)
                    {
                        if (p.EnsureToken(Token.TokenType.Comma) == null) return null;
                    }
                    first = false;

                    Expression val = Parse(e, p);
                    if (val == null) return null;
                    e.func.parameters.Add(val);
                }
                if (p.EnsureToken(Token.TokenType.CloseParen) == null) return null;

                return e;
            }

            Parser.ParserError.ReportSync(p, "Expected expression", p.tokenStream.Peek().line);
            return null;
        }

        /*public static Value Evaluate(Expression e)
        {
            Expression optimized = Optimize(e);
            if (optimized.value == null && optimized.arrayValues == null)
            {
                Parser.ParserError.Report("Expected constant expression", -1);
                return null;
            }
            if (optimized.arrayValues != null)
            {
                CheckArray(optimized.arrayValues);
                return new Value(optimized.arrayValues);
            }
            return optimized.value;
        }

        private static void CheckArray(List<Expression> exprs)
        {
            foreach (Expression v in exprs)
            {
                if (v.value == null && v.arrayValues == null)
                {
                    Parser.ParserError.Report("Expected constant expression", -1);
                    return;
                }
                if (v.arrayValues != null)
                    CheckArray(v.arrayValues);
            }
        }

        public static Expression Optimize(Expression e, bool constantsOnly = false)
        {
            if (e.value != null)
                return e;
            if (e.arrayValues != null)
            {
                List<Expression> optimized = new List<Expression>();
                foreach (Expression toOptimize in e.arrayValues)
                {
                    optimized.Add(Optimize(toOptimize));
                }
                return new Expression(null, optimized, null);
            }

            Expression optimizedLeft = Optimize(e.func.parameters[0]);
            if (e.func.function.parameterCount == 2)
            {
                Expression optimizedRight = Optimize(e.func.parameters[1]);
            } else
            {
                if (optimizedLeft.value == null)
                {
                    return optimizedLeft;
                }
                if (optimizedLeft.value.type == Value.Type.Variable)
                {
                    return optimizedLeft;
                }
                switch (e.func.function.name)
                {
                    case "-":
                        break;
                    case "!":
                        break;
                }
            }
        }*/
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
            RawIdentifier = 6, // Special case
            //Array = 7
        }
        public Type type;
        public double valueDouble;
        public int valueInt32;
        public string valueString;
        public bool valueBoolean;
        public string valueVariable;
        public string valueRawIdentifier;
        //public List<Value> valueArray;

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
            Token t = parser.EnsureToken(Token.TokenType.Number, Token.TokenType.String, Token.TokenType.VariableIdentifier, Token.TokenType.True, Token.TokenType.False, Token.TokenType.Undefined);
            if (t == null) return;
            FromToken(t);
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

        public Value(string str) : base(null, null)
        {
            type = Type.String;
            valueString = str;
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
