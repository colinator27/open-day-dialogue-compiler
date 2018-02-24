using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDayDialogue
{
    class Function
    {
        public string name;
        public int parameterCount;

        public Function(string name, int parameterCount)
        {
            this.name = name;
            this.parameterCount = parameterCount;
        }
    }

    static class BuiltinFunctions
    {
        static Dictionary<string, Function> functions;
        static bool initialized = false;

        public static void Initialize()
        {
            functions = new Dictionary<string, Function>()
            {
                { "+", new Function("+", 2) },
                { "-", new Function("-", 2) },
                { "*", new Function("*", 2) },
                { "/", new Function("/", 2) },
                { "%", new Function("%", 2) },
                { "||", new Function("||", 2) },
                { "&&", new Function("&&", 2) },
                { "^^", new Function("^^", 2) },
                { "==", new Function("==", 2) },
                { "!=", new Function("!=", 2) },
                { ">=", new Function(">=", 2) },
                { "<=", new Function("<=", 2) },
                { "<", new Function("<", 2) },
                { ">", new Function(">", 2) },
                { "!", new Function("!", 1) }
            };
        }

        public static Function Get(string name)
        {
            if (!initialized)
                Initialize();
            return functions[name];
        }

        public static bool IsBuiltin(string name)
        {
            return functions.ContainsKey(name);
        }
    }
}
