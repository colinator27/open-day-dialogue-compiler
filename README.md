# open-day-dialogue-compiler
Compiler for the Open Day Dialogue programming language.

To use, simply build the project as an executable, and run it from command line. You should be prompted with the usage.

As of writing/updating this, the usage is as follows:
```
OpenDayDialogue <options>
Options:
  -s, --source=file          The input source file name/path.
  -e, --export=file          The export binary file name/path. Used in
                               conjunction with '--source' option.
  -d, --debug                Will emit debug instructions when compiling,
                               useful for debugging with an interpreter.
      --make-translations    Generate translation files as compiling happens.
                               Outputs to the same directory as source files,
                               with extension '.opdat'.
      --exclude-values       Exclude values/commands when generating
                               translations (has no effect when applying the
                               translation files).
      --apply-translations   Will apply translation files if they are found
                               with the source code files. They must be the
                               source file's name followed by '.opdat'.
      --ignore-hash          When applying translation files, ignore the
                               original file hash. Warning: This can be risky.
      --show-instructions    Show the final list of instructions when the
                               program finishes compiling.
      --err-log-items        When an error gets logged, it will include the
                               item's name if possible.
  -h, --help                 Show this help menu.
```

Note:
This compiler's design was heavily inspired by YarnSpinner, especially some parts of `Parser.cs`.
