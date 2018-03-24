using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDayDialogue
{
    class CodeError
    {
        public string message;
        public string line;
        public string module;
        public Severity severity;
        public string file;
        public string itemName;

        public enum Severity
        {
            Warning,
            Error,
            ErrorDeadly
        }
    }

    static class Errors
    {
        public static List<CodeError> errors = new List<CodeError>();

        /// <summary>
        /// Reports an error into the error list.
        /// </summary>
        /// <param name="message">The message of the error.</param>
        /// <param name="line">The approximate on which the error occurred.</param>
        /// <param name="severity">How severe the error is.</param>
        /// <param name="module">The compilation module where the error was detected.</param>
        public static void Report(string message, string line, CodeError.Severity severity = CodeError.Severity.Error, string module = "", string itemName = "")
        {
            errors.Add(new CodeError()
            {
                message = message,
                line = line,
                severity = severity,
                module = module,
                file = Application.currentFile,
                itemName = itemName
            });
        }

        /// <summary>
        /// Determines whether or not the compilation process should continue.
        /// </summary>
        /// <returns>Returns true or false.</returns>
        public static bool CanContinue()
        {
            foreach (CodeError e in errors)
            {
                if (e.severity == CodeError.Severity.ErrorDeadly)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Compiles a list of every error.
        /// </summary>
        /// <returns>The string value.</returns>
        public static string GetErrors()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Error list:");
            sb.AppendLine();
            foreach (CodeError e in errors)
            {
                sb.AppendFormat("File \"{1}\", line {2}, in {3}: {4} {5}({0})", 
                        (e.severity == CodeError.Severity.Error || e.severity == CodeError.Severity.ErrorDeadly) ? "Error" : "Warn",
                        e.file,
                        e.line,
                        e.module,
                        e.message,
                        (Application.errorsLogItems && e.itemName != null && e.itemName != "") ? string.Format("(item name: \"{0}\") ", e.itemName) : "");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
