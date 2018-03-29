using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDayDialogue
{
    class Program
    {
        public Dictionary<string/* string content */, uint/* id */> stringEntries = new Dictionary<string, uint>();
        public Dictionary<uint/* id */, CommandCall> commandTable = new Dictionary<uint, CommandCall>();
        public Dictionary<uint/* scene name id */, uint/* label id */> scenes = new Dictionary<uint, uint>();
        public Dictionary<uint/* key string id */, uint/* value string id */> definitions = new Dictionary<uint, uint>();
        public Dictionary<uint/* value id */, Value> values = new Dictionary<uint, Value>();
        public Dictionary<string/* custom label name */, uint/* label id */> customLabels = new Dictionary<string, uint>();
        public List<Instruction> instructions = new List<Instruction>();

        private uint labelCounter = 0;
        /// <summary>
        /// Generates a new label ID.
        /// </summary>
        /// <returns>The next label ID</returns>
        public uint GetNextLabel()
        {
            return labelCounter++;
        }

        private uint stringCounter = 0;
        /// <summary>
        /// Registers a new string in the program, or finds a duplicate.
        /// </summary>
        /// <param name="s">The string content</param>
        /// <returns>The string ID</returns>
        public uint RegisterString(string s)
        {
            if (stringEntries.ContainsKey(s))
            {
                return stringEntries[s];
            } else
            {
                stringEntries[s] = stringCounter;
                return stringCounter++;
            }
        }

        private uint valueCounter = 0;
        /// <summary>
        /// Registers a value to the program, or finds a duplicate.
        /// </summary>
        /// <param name="v">The value to register</param>
        /// <param name="c">The compiler</param>
        /// <returns>The value ID</returns>
        public uint RegisterValue(Value v, Compiler c)
        {
            uint id = valueCounter++;
            if (v.type == Value.Type.RawIdentifier)
            {
                v.stringID = RegisterString(v.valueRawIdentifier);
            }
            else if (v.type == Value.Type.String)
            {
                if (!Application.excludeValues)
                {
                    if (Application.generateTranslations)
                    {
                        Application.genTranslations[Application.currentFile].Item2["s:" + c.GetCurrentNamespace()].Add(v.valueString);
                    }
                    else if (Application.applyTranslations)
                    {
                        if (Application.queuedTranslations[Application.currentFile].Item2.ContainsKey("s:" + c.GetCurrentNamespace()))
                        {
                            Queue<string> q = Application.queuedTranslations[Application.currentFile].Item2["s:" + c.GetCurrentNamespace()];
                            if (q.Count == 0)
                            {
                                c.Error("Translation file string count does not match with the actual code!");
                                return 0;
                            }
                            v.valueString = q.Dequeue();
                        }
                    }
                }
                v.stringID = RegisterString(v.valueString);
            }
            else if (v.type == Value.Type.Variable)
            {
                v.stringID = RegisterString(v.valueVariable);
            }
            if (values.ContainsValue(v))
                return values.FirstOrDefault(x => x.Value.Equals(v)).Key;
            values.Add(id, v);
            return id;
        }

        private uint commandCounter = 0;
        /// <summary>
        /// Registers a new command call to the program, or finds a duplicate.
        /// </summary>
        /// <param name="c">The command call</param>
        /// <returns>The command call ID</returns>
        public uint RegisterCommand(CommandCall c)
        {
            uint id = commandCounter++;
            if (commandTable.ContainsValue(c))
                return commandTable.FirstOrDefault(x => x.Value.Equals(c)).Key;
            commandTable.Add(id, c);
            return id;
        }

        /// <summary>
        /// Randomly shuffles the strings in the string table, to make it less decipherable.
        /// </summary>
        public void ShuffleStrings()
        {
            Random r = new Random();
            stringEntries = stringEntries.OrderBy(x => r.Next())
               .ToDictionary(item => item.Key, item => item.Value);
        }
    }

    class CommandCall
    {
        public uint nameStringID;
        public uint[] argValueIDs;

        public override bool Equals(object obj)
        {
            if (obj.GetType() != GetType())
                return false;
            CommandCall other = (CommandCall)obj;
            if (other.nameStringID != nameStringID)
                return false;
            if (other.argValueIDs.Length != argValueIDs.Length)
                return false;
            for (int i = 0; i < argValueIDs.Length; i++)
            {
                if (argValueIDs[i] != other.argValueIDs[i])
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(nameStringID, argValueIDs).GetHashCode();
        }
    }

    class Instruction
    {
        public enum Opcode
        {
            // Global opcodes
            Nop = 0x00,
            Label = 0x01, // operand1: label id

            // Stack operations
            Push = 0xA0, // operand1: value id
            Pop = 0xA1,
            Convert = 0xA2, // operand1: short number of new type, of Value.Type

            // Builtin operators
            BOAdd = 0xC0,
            BOSub = 0xC1,
            BOMul = 0xC2,
            BODiv = 0xC3,
            BOMod = 0xC4,
            BOEqual = 0xC5,
            BONotEqual = 0xC6,
            BOGreater = 0xC7,
            BOGreaterEqual = 0xC8,
            BOLessThan = 0xC9,
            BOLessThanEqual = 0xCA,
            BONegate = 0xCB,
            BOOr = 0xCC,
            BOXor = 0xCD,
            BOAnd = 0xCE,
            BOInvert = 0xCF,

            // Scene opcodes
            Jump = 0xB0, // operand1: label id
            Exit = 0xB1, // exits current scene
            TextRun = 0xB2, // operand1: string id
            CommandRun = 0xB3, // operand1: command id from command table
            SetVariable = 0xB4, // operand1: variable name id. Uses top value on stack
            CallFunction = 0xB5, // operand1: function name id. Used for non-builitin functions

            // jump if top val of stack is true, pop it off
            JumpTrue = 0xB6, // operand1: label id

            // jump if top val of stack is false, pop it off
            JumpFalse = 0xB7, // operand1: label id

            // be ready to read choices
            BeginChoice = 0xB8,

            // add choice
            Choice = 0xB9, // operand1: string id, operand2: label id for the choice

            // if the top val of stack is true, pop it off, add choice
            ChoiceTrue = 0xBA, // operand1: string id, operand2: label id for the choice

            // wait for user input, goes to one of the choices, and if no conditions match/there are none, go to
            // the end specified in operand1
            ChoiceSelection = 0xBB,

            // set the debug line for interpreter, not always emitted (based on flag)
            DebugLine = 0xD0 // operand1: line number
        }

        public Opcode opcode;
        public uint? operand1;
        public uint? operand2;

        public string ToString(Program p)
        {
            string build = opcode.ToString() + " ";
            switch (opcode)
            {
                case Opcode.Push:
                    build += p.values[(uint)operand1].ToString();
                    break;
                case Opcode.TextRun:
                    build += "\"" + p.stringEntries.FirstOrDefault(x => x.Value == operand1).Key + "\"";
                    break;
                case Opcode.Choice:
                case Opcode.ChoiceTrue:
                    build += "\"" + p.stringEntries.FirstOrDefault(x => x.Value == (uint)operand1).Key + "\" " + operand2.ToString();
                    break;
                default:
                    if (operand1 != null)
                        build += operand1.ToString();
                    if (operand2 != null)
                        build += " " + operand2.ToString();
                    break;
            }
            return build;
        }
    }

    class Compiler
    {
        public Program program;
        private Stack<string> currentNamespace;
        private Stack<Tuple<SceneWhileLoop, uint/*begin label*/, uint/*end label*/>> whileLoops;
        private List<string> sceneDefinedLabels;
        private List<string> sceneReferencedLabels;

        /// <summary>
        /// Reports an error to the error list.
        /// </summary>
        public void Error(string message)
        {
            Errors.Report(message, "?", CodeError.Severity.ErrorDeadly, "Compiler", GetCurrentNamespace());
        }

        public Compiler()
        {
            program = new Program();
            currentNamespace = new Stack<string>();
            whileLoops = new Stack<Tuple<SceneWhileLoop, uint, uint>>();
            sceneDefinedLabels = new List<string>();
            sceneReferencedLabels = new List<string>();
        }

        /// <summary>
        /// Gets the current namespace, down to the current scene or definition group if it exists.
        /// </summary>
        /// <returns>String of the namespace</returns>
        public string GetCurrentNamespace()
        {
            return string.Join(".", currentNamespace.Reverse());
        }

        /// <summary>
        /// Gets a full symbol name (with namespace) for an item, such as a definition key symbol.
        /// </summary>
        /// <param name="name">The item</param>
        /// <returns>String of the full item name</returns>
        public string GetFullSymbolName(string name)
        {
            string n = GetCurrentNamespace();
            return n != "" ? (n + "." + name) : name;
        }

        public void GenerateCode(Block block)
        {
            if (block.parent == null)
                GenerateCustomLabels(block);

            // Generate code for each statement
            foreach (Statement s in block.statements)
            {
                GenerateCode(s);
            }
        }

        public void GenerateCustomLabels(Block block)
        {
            foreach (Statement s in block.statements)
            {
                switch (s.type)
                {
                    case Statement.Type.Block:
                        GenerateCustomLabels(s.block);
                        break;
                    case Statement.Type.Namespace:
                        GenerateCustomLabels(s.@namespace.block);
                        break;
                    case Statement.Type.Scene:
                        s.scene.statements.ForEach(sc => GenerateCustomLabels(sc));
                        break;
                }
            }
        }

        public void GenerateCustomLabels(SceneStatement s)
        {
            switch (s.type)
            {
                case SceneStatement.Type.ChoiceStatement:
                    foreach (Choice c in s.choiceStatement.choices)
                    {
                        c.statements.ForEach(sc => GenerateCustomLabels(sc));
                    }
                    break;
                case SceneStatement.Type.IfStatement:
                    s.ifStatement.mainClause.statements.ForEach(sc => GenerateCustomLabels(sc));
                    foreach (Clause c in s.ifStatement.elseClauses)
                    {
                        c.statements.ForEach(sc => GenerateCustomLabels(sc));
                    }
                    break;
                case SceneStatement.Type.WhileLoop:
                    s.whileLoop.statements.ForEach(sc => GenerateCustomLabels(sc));
                    break;
                case SceneStatement.Type.Label:
                    program.customLabels[s.label.name] = program.GetNextLabel();
                    break;
            }
        }

        public void GenerateCode(Statement s)
        {
            switch (s.type)
            {
                case Statement.Type.Block:
                    GenerateCode(s.block);
                    break;
                case Statement.Type.DefinitionGroup:
                    currentNamespace.Push(s.definitionGroup.groupName);
                    if (Application.generateTranslations)
                    {
                        Application.genTranslations[Application.currentFile].Item2["d:" + GetCurrentNamespace()] = new List<string>();
                    }
                    foreach (TextDefinition def in s.definitionGroup.definitions)
                    {
                        uint keyID = program.RegisterString(GetFullSymbolName(def.key));
                        if (program.definitions.ContainsKey(keyID))
                        {
                            Error(string.Format("Found duplicate definitions for {0}", GetFullSymbolName(def.key)));
                            return;
                        }
                        if (Application.generateTranslations)
                        {
                            Application.genTranslations[Application.currentFile].Item2["d:" + GetCurrentNamespace()].Add(def.value); 
                        } else if (Application.applyTranslations)
                        {
                            if (Application.queuedTranslations[Application.currentFile].Item2.ContainsKey("d:" + GetCurrentNamespace()))
                            {
                                Queue<string> q = Application.queuedTranslations[Application.currentFile].Item2["d:" + GetCurrentNamespace()];
                                if (q.Count == 0)
                                {
                                    Error(string.Format("Translation file string count does not match with the actual code!\nItem identifier: {0}",
                                                                      "d:" + GetCurrentNamespace()));
                                    return;
                                }
                                def.value = q.Dequeue();
                            }
                        }
                        uint valueID = program.RegisterString(def.value);
                        program.definitions[keyID] = valueID;
                    }
                    if (Application.applyTranslations)
                    {
                        if (Application.queuedTranslations[Application.currentFile].Item2.ContainsKey("d:" + GetCurrentNamespace()))
                        {
                            if (Application.queuedTranslations[Application.currentFile].Item2["d:" + GetCurrentNamespace()].Count != 0)
                            {
                                Error(string.Format("Translation file string count does not match with the actual code!\nItem identifier: {0}",
                                                                  "d:" + GetCurrentNamespace()));
                                return;
                            }
                        }
                    }
                    currentNamespace.Pop();
                    break;
                case Statement.Type.Namespace:
                    currentNamespace.Push(s.@namespace.name);
                    GenerateCode(s.@namespace.block);
                    currentNamespace.Pop();
                    break;
                case Statement.Type.Scene:
                    currentNamespace.Push(s.scene.name);
                    sceneDefinedLabels.Clear();
                    sceneReferencedLabels.Clear();
                    if (Application.generateTranslations)
                    {
                        Application.genTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()] = new List<string>();
                    }
                    GenerateCode(s.scene);
                    if (Application.applyTranslations)
                    {
                        if (Application.queuedTranslations[Application.currentFile].Item2.ContainsKey("s:" + GetCurrentNamespace()))
                        {
                            if (Application.queuedTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()].Count != 0)
                            {
                                Error(string.Format("Translation file string count does not match with the actual code!\nItem identifier: {0}",
                                                                  "s:" + GetCurrentNamespace()));
                                return;
                            }
                        }
                    }
                    foreach (string l in sceneReferencedLabels)
                    {
                        string match = sceneDefinedLabels.Find(x => (x == l));
                        if (match == null)
                        {
                            Errors.Report("Found a jump statement that jumps to a label from another scene.", "?", CodeError.Severity.Warning, "Compiler", GetCurrentNamespace());
                        }
                    }
                    sceneReferencedLabels.Clear();
                    sceneDefinedLabels.Clear();
                    currentNamespace.Pop();
                    break;
            }
        }

        public void GenerateCode(Scene scene)
        {
            if (program.scenes.ContainsKey(program.RegisterString(GetCurrentNamespace())))
            {
                Error(string.Format("Only one definition of a scene is permitted. Scene name: \"{0}\"", GetCurrentNamespace()));
                return;
            }

            // Add the label
            uint labelID = program.GetNextLabel();
            program.scenes[program.RegisterString(GetCurrentNamespace())] = labelID;
            Emit(Instruction.Opcode.Label, labelID);

            // Generate code for the scene statements
            foreach (SceneStatement s in scene.statements)
            {
                GenerateCode(s);
            }

            // Make sure interpreter doesn't move to another scene
            Emit(Instruction.Opcode.Exit);
        }

        public void GenerateCode(SceneStatement statement)
        {
            switch (statement.type)
            {
                case SceneStatement.Type.Text:
                    // Translations
                    if (Application.generateTranslations)
                    {
                        Application.genTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()].Add(statement.text.text); 
                    } else if (Application.applyTranslations)
                    {
                        if (Application.queuedTranslations[Application.currentFile].Item2.ContainsKey("s:" + GetCurrentNamespace()))
                        {
                            Queue<string> q = Application.queuedTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()];
                            if (q.Count == 0)
                            {
                                Error(string.Format("Translation file string count does not match with the actual code!\nItem identifier: {0}",
                                                                  "s:" + GetCurrentNamespace()));
                                return;
                            }
                            statement.text.text = q.Dequeue();
                        }
                    }

                    // Debug instruction
                    if (statement.text.sceneLine != null)
                        DebugEmitLine((int)statement.text.sceneLine);

                    // Emit the instruction
                    Emit(Instruction.Opcode.TextRun, program.RegisterString(statement.text.text));
                    break;
                case SceneStatement.Type.Command:
                    // Register values
                    List<uint> valueIDs = new List<uint>();
                    foreach (Value v in statement.command.args.Skip(1))
                        valueIDs.Add(program.RegisterValue(v, this));

                    // Register the command call
                    uint commandID = program.RegisterCommand(new CommandCall()
                    {
                        nameStringID = program.RegisterString(statement.command.args[0].valueRawIdentifier),
                        argValueIDs = valueIDs.ToArray()
                    });

                    // Debug instruction
                    if (statement.command.sceneLine != null)
                        DebugEmitLine((int)statement.command.sceneLine);

                    // Emit the instruction
                    Emit(Instruction.Opcode.CommandRun, commandID);
                    break;
                case SceneStatement.Type.VariableAssign:
                    // Debug instruction
                    if (statement.variableAssign.sceneLine != null)
                        DebugEmitLine((int)statement.variableAssign.sceneLine);

                    // Compile the expression
                    GenerateCode(statement.variableAssign.value);

                    // Set the variable
                    Emit(Instruction.Opcode.SetVariable, program.RegisterString(statement.variableAssign.variableName));
                    break;
                case SceneStatement.Type.IfStatement:
                    // Allocate labels and assign them to clauses
                    int numberOfClauses = statement.ifStatement.elseClauses.Count + 1;
                    List<KeyValuePair<Clause, uint>> clauses = new List<KeyValuePair<Clause, uint>>();
                    for (int i = 0; i < numberOfClauses; i++)
                    {
                        if (i == 0)
                        {
                            clauses.Add(new KeyValuePair<Clause, uint>(statement.ifStatement.mainClause, program.GetNextLabel()));
                        } else
                        {
                            clauses.Add(new KeyValuePair<Clause, uint>(statement.ifStatement.elseClauses[i - 1], program.GetNextLabel()));
                        }
                    }

                    // Debug instruction
                    if (statement.ifStatement.sceneLine != null)
                        DebugEmitLine((int)statement.ifStatement.sceneLine);

                    // Generate instructions for each clause, including the main one
                    for (int i = 0; i < clauses.Count; i++)
                    {
                        Clause c = clauses[i].Key;
                        if (c.expression != null)
                        {
                            // The if condition
                            GenerateCode(c.expression);

                            // If the condition is false, jump to the label which signals the end of this clause
                            Emit(Instruction.Opcode.JumpFalse, clauses[i].Value);

                            // Write the statements for this clause
                            c.statements.ForEach(s => GenerateCode(s));

                            // If this isn't the final clause, jump to the end of the whole thing
                            if (i + 1 != clauses.Count)
                            {
                                Emit(Instruction.Opcode.Jump, clauses[clauses.Count - 1].Value);
                            }
                        } else
                        {
                            // Final else clause, with no condition, simply write the statements.
                            c.statements.ForEach(s => GenerateCode(s));
                        }
                        // Write the label that signals the end of this clause
                        Emit(Instruction.Opcode.Label, clauses[i].Value);
                    }
                    break;
                case SceneStatement.Type.ChoiceStatement:
                    // Debug instruction
                    if (statement.choiceStatement.sceneLine != null)
                        DebugEmitLine((int)statement.choiceStatement.sceneLine);

                    Emit(Instruction.Opcode.BeginChoice);
                    uint endChoiceLabel = program.GetNextLabel();

                    // Allocate labels and assign them to choices
                    List<KeyValuePair<Choice, uint>> choices = new List<KeyValuePair<Choice, uint>>();
                    for (int i = 0; i < statement.choiceStatement.choices.Count; i++)
                    {
                        choices.Add(new KeyValuePair<Choice, uint>(statement.choiceStatement.choices[i], program.GetNextLabel()));
                    }

                    // Write the instructions determining what/where the choices are
                    for (int i = 0; i < choices.Count; i++)
                    {
                        Choice c = choices[i].Key;
                        if (c.condition != null)
                        {
                            // Generate expression bytecode for whether or not choice is available

                            GenerateCode(c.condition);

                            // Translations
                            if (Application.generateTranslations)
                            {
                                Application.genTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()].Add(c.choiceText);
                            } else if (Application.applyTranslations)
                            {
                                if (Application.queuedTranslations[Application.currentFile].Item2.ContainsKey("s:" + GetCurrentNamespace()))
                                {
                                    Queue<string> q = Application.queuedTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()];
                                    if (q.Count == 0)
                                    {
                                        Error(string.Format("Translation file string count does not match with the actual code!\nItem identifier: {0}",
                                                                  "s:" + GetCurrentNamespace()));
                                        return;
                                    }
                                    c.choiceText = q.Dequeue();
                                }
                            }

                            // Write the choice instruction
                            Emit(Instruction.Opcode.ChoiceTrue, program.RegisterString(c.choiceText), choices[i].Value);
                        } else
                        {
                            // Translations
                            if (Application.generateTranslations)
                            {
                                Application.genTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()].Add(c.choiceText);
                            } else if (Application.applyTranslations)
                            {
                                if (Application.queuedTranslations[Application.currentFile].Item2.ContainsKey("s:" + GetCurrentNamespace()))
                                {
                                    Queue<string> q = Application.queuedTranslations[Application.currentFile].Item2["s:" + GetCurrentNamespace()];
                                    if (q.Count == 0)
                                    {
                                        Error(string.Format("Translation file string count does not match with the actual code!\nItem identifier: {0}",
                                                                  "s:" + GetCurrentNamespace()));
                                        return;
                                    }
                                    c.choiceText = q.Dequeue();
                                }
                            }

                            // Write the choice instruction
                            Emit(Instruction.Opcode.Choice, program.RegisterString(c.choiceText), choices[i].Value);
                        }
                    }

                    // Allow the interpreter to get input and navigate to one of the labels
                    Emit(Instruction.Opcode.ChoiceSelection, endChoiceLabel);

                    // Write the statements for each choice, as well as their labels
                    for (int i = 0; i < choices.Count; i++)
                    {
                        Choice c = choices[i].Key;
                        
                        Emit(Instruction.Opcode.Label, choices[i].Value);
                        c.statements.ForEach(s => GenerateCode(s));
                        Emit(Instruction.Opcode.Jump, endChoiceLabel);
                    }

                    // Write the label that signals the end of the choices                   
                    Emit(Instruction.Opcode.Label, endChoiceLabel);
                    break;
                case SceneStatement.Type.SpecialName:
                    GenerateCode(new SceneStatement(null, null, SceneStatement.Type.Command)
                    {
                        command = statement.command
                    });
                    GenerateCode(new SceneStatement(null, null, SceneStatement.Type.Text)
                    {
                        text = statement.text
                    });
                    break;
                case SceneStatement.Type.Label:
                    // Debug instruction
                    if (statement.label.sceneLine != null)
                        DebugEmitLine((int)statement.label.sceneLine);

                    if (!program.customLabels.ContainsKey(statement.label.name))
                    {
                        Error(string.Format("Somehow, the compiler failed to register a label ID for label \"{0}\". This shouldn't happen.", statement.label.name));
                        return;
                    }
                    Emit(Instruction.Opcode.Label, program.customLabels[statement.label.name]);

                    sceneDefinedLabels.Add(statement.label.name);
                    break;
                case SceneStatement.Type.Jump:
                    // Debug instruction
                    if (statement.jump.sceneLine != null)
                        DebugEmitLine((int)statement.jump.sceneLine);

                    if (!program.customLabels.ContainsKey(statement.jump.labelName))
                    {
                        Error(string.Format("Failed to find label with name \"{0}\". Invalid jump statement.", statement.jump.labelName));
                        return;
                    }
                    Emit(Instruction.Opcode.Jump, program.customLabels[statement.jump.labelName]);

                    sceneReferencedLabels.Add(statement.jump.labelName);
                    break;
                case SceneStatement.Type.WhileLoop:
                    // Debug instruction
                    if (statement.whileLoop.sceneLine != null)
                        DebugEmitLine((int)statement.whileLoop.sceneLine);

                    // Get labels pre-generated
                    uint beginConditionLabel = program.GetNextLabel();
                    uint endLoopLabel = program.GetNextLabel();

                    // Enter this loop's context
                    whileLoops.Push(new Tuple<SceneWhileLoop, uint, uint>(statement.whileLoop, beginConditionLabel, endLoopLabel));

                    // Condition to test for each iteration
                    Emit(Instruction.Opcode.Label, beginConditionLabel);
                    GenerateCode(statement.whileLoop.condition);

                    // If the condition is false, jump to the end
                    Emit(Instruction.Opcode.JumpFalse, endLoopLabel);

                    // Write the statements to run each iteration
                    statement.whileLoop.statements.ForEach(s => GenerateCode(s));

                    // After each iteration, jump to the condition check for the next iteration
                    Emit(Instruction.Opcode.Jump, beginConditionLabel);

                    // The end of the loop
                    Emit(Instruction.Opcode.Label, endLoopLabel);

                    // Exit this loop's context
                    whileLoops.Pop();

                    break;
                case SceneStatement.Type.ControlFlow:
                    // Debug instruction
                    if (statement.controlFlow.sceneLine != null)
                        DebugEmitLine((int)statement.controlFlow.sceneLine);

                    switch (statement.controlFlow.content)
                    {
                        case "continue":
                            Emit(Instruction.Opcode.Jump, whileLoops.Peek().Item2);
                            break;
                        case "break":
                            Emit(Instruction.Opcode.Jump, whileLoops.Peek().Item3);
                            break;
                        default:
                            Error("Invalid control flow keyword.");
                            return;
                    }
                    break;
            }
        }

        public void GenerateCode(Expression e)
        {
            if (e.value != null)
            {
                // Standard value
                Emit(Instruction.Opcode.Push, program.RegisterValue(e.value, this));
            } else
            {
                if (e.func.parameters.Count == 1 && e.func.function.name == "-")
                {
                    // Turn things like "Push 4, BONegate" into "Push -4"
                    // Unfortunately it only goes one level so this may require updating
                    if (e.func.parameters[0].value != null)
                    {
                        Value v = e.func.parameters[0].value;
                        bool ok = false;
                        if (v.type == Value.Type.Int32)
                        {
                            v.valueInt32 = -v.valueInt32;
                            ok = true;
                        }
                        else if (v.type == Value.Type.Double)
                        {
                            v.valueDouble = -v.valueDouble;
                            ok = true;
                        }

                        // If the type is valid, emit it here
                        if (ok)
                        {
                            Emit(Instruction.Opcode.Push, program.RegisterValue(v, this));
                            return;
                        }
                    }
                } else if (e.func.parameters.Count == 2)
                {
                    // Perform one small level of constant propagation
                    if (e.func.parameters[0].value != null && e.func.parameters[1].value != null)
                    {
                        if (e.func.parameters[0].value.type == e.func.parameters[1].value.type)
                        {
                            Value v = e.func.parameters[0].value;
                            bool ok = true;
                            switch (e.func.function.name)
                            {
                                case "+":
                                    switch (v.type)
                                    {
                                        case Value.Type.Int32:
                                            v.valueInt32 += e.func.parameters[1].value.valueInt32;
                                            break;
                                        case Value.Type.Double:
                                            v.valueDouble += e.func.parameters[1].value.valueDouble;
                                            break;
                                        case Value.Type.String:
                                            v.valueString += e.func.parameters[1].value.valueString;
                                            break;
                                        default:
                                            ok = false;
                                            break;
                                    }
                                    break;
                                case "-":
                                    switch (v.type)
                                    {
                                        case Value.Type.Int32:
                                            v.valueInt32 -= e.func.parameters[1].value.valueInt32;
                                            break;
                                        case Value.Type.Double:
                                            v.valueDouble -= e.func.parameters[1].value.valueDouble;
                                            break;
                                        default:
                                            ok = false;
                                            break;
                                    }
                                    break;
                                case "*":
                                    switch (v.type)
                                    {
                                        case Value.Type.Int32:
                                            v.valueInt32 *= e.func.parameters[1].value.valueInt32;
                                            break;
                                        case Value.Type.Double:
                                            v.valueDouble *= e.func.parameters[1].value.valueDouble;
                                            break;
                                        default:
                                            ok = false;
                                            break;
                                    }
                                    break;
                                case "/":
                                    switch (v.type)
                                    {
                                        case Value.Type.Int32:
                                            v.valueInt32 /= e.func.parameters[1].value.valueInt32;
                                            break;
                                        case Value.Type.Double:
                                            v.valueDouble /= e.func.parameters[1].value.valueDouble;
                                            break;
                                        default:
                                            ok = false;
                                            break;
                                    }
                                    break;
                                case "==":
                                    switch (v.type)
                                    {
                                        case Value.Type.Int32:
                                            v.type = Value.Type.Boolean;
                                            v.valueBoolean = (v.valueInt32 == e.func.parameters[1].value.valueInt32);
                                            break;
                                        case Value.Type.Double:
                                            v.type = Value.Type.Boolean;
                                            v.valueBoolean = (v.valueDouble == e.func.parameters[1].value.valueDouble);
                                            break;
                                        case Value.Type.String:
                                            v.type = Value.Type.String;
                                            v.valueBoolean = (v.valueString == e.func.parameters[1].value.valueString);
                                            break;
                                        default:
                                            ok = false;
                                            break;
                                    }
                                    break;
                                case "!=":
                                    switch (v.type)
                                    {
                                        case Value.Type.Int32:
                                            v.type = Value.Type.Boolean;
                                            v.valueBoolean = (v.valueInt32 != e.func.parameters[1].value.valueInt32);
                                            break;
                                        case Value.Type.Double:
                                            v.type = Value.Type.Boolean;
                                            v.valueBoolean = (v.valueDouble != e.func.parameters[1].value.valueDouble);
                                            break;
                                        case Value.Type.String:
                                            v.type = Value.Type.String;
                                            v.valueBoolean = (v.valueString != e.func.parameters[1].value.valueString);
                                            break;
                                        default:
                                            ok = false;
                                            break;
                                    }
                                    break;
                                default:
                                    ok = false;
                                    break;
                            }
                            if (ok)
                            {
                                Emit(Instruction.Opcode.Push, program.RegisterValue(v, this));
                                return;
                            }
                        }
                    }
                }

                // Function call
                foreach (Expression param in e.func.parameters)
                {
                    GenerateCode(param);
                }

                if (e.func.function.parameterCount == -1)
                {
                    // Non-native/non-builtin function; push the number of parameters passed
                    Emit(Instruction.Opcode.Push, 
                        program.RegisterValue(new Value(null, null, new Token()
                        {
                            content = e.func.parameters.Count.ToString(),
                            type = Token.TokenType.Number
                        })
                    , this));
                    Emit(Instruction.Opcode.CallFunction, program.RegisterString(e.func.function.name));
                } else
                {
                    // Native function, use their own opcodes. Find it and emit it.
                    Instruction.Opcode op;
                    switch (e.func.function.name)
                    {
                        case "+": op = Instruction.Opcode.BOAdd; break;
                        case "-":
                            if (e.func.parameters.Count == 1)
                                op = Instruction.Opcode.BONegate;
                            else
                                op = Instruction.Opcode.BOSub;
                            break;
                        case "*": op = Instruction.Opcode.BOMul; break;
                        case "/": op = Instruction.Opcode.BODiv; break;
                        case "%": op = Instruction.Opcode.BOMod; break;
                        case "||": op = Instruction.Opcode.BOOr; break;
                        case "&&": op = Instruction.Opcode.BOAnd; break;
                        case "^^": op = Instruction.Opcode.BOXor; break;
                        case "==": op = Instruction.Opcode.BOEqual; break;
                        case "!=": op = Instruction.Opcode.BONotEqual; break;
                        case ">=": op = Instruction.Opcode.BOGreaterEqual; break;
                        case "<=": op = Instruction.Opcode.BOLessThanEqual; break;
                        case "<": op = Instruction.Opcode.BOLessThan; break;
                        case ">": op = Instruction.Opcode.BOGreater; break;
                        case "!": op = Instruction.Opcode.BOInvert; break;
                        default: Error("Invalid function"); return;
                    }
                    Emit(op);
                }
            }
        }

        public string GetInstructionsString()
        {
            string build = "";
            program.instructions.ForEach(i => { build += i.ToString(program) + "\n"; });
            return build;
        }

        /// <summary>
        /// Emits an instruction to the instruction list.
        /// </summary>
        /// <param name="op">The opcode</param>
        /// <param name="operand1">The first operand, if necessary</param>
        /// <param name="operand2">The second operand, if necessary</param>
        void Emit(Instruction.Opcode op, uint? operand1 = null, uint? operand2 = null)
        {
            Instruction i = new Instruction()
            {
                opcode = op,
                operand1 = operand1,
                operand2 = operand2
            };

            program.instructions.Add(i);
        }

        /// <summary>
        /// If enabled, will emit a debug instruction indicating the line in the source code that the code is running at.
        /// </summary>
        /// <param name="line">The line number</param>
        void DebugEmitLine(int line)
        {
            if (Application.emitDebugInstructions)
                Emit(Instruction.Opcode.DebugLine, (uint)line);
        }
    }
}
