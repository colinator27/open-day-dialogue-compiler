using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenDayDialogue
{
    class BytecodeWriter
    {
        Program program;

        public BytecodeWriter(Program program)
        {
            this.program = program;
        }

        public void Write(Stream s)
        {
            using (LEBinaryWriter bw = new LEBinaryWriter(s, Encoding.UTF8))
            {
                bw.Write(new char[] { 'O', 'P', 'D', 'A' });

                // New as of version >= 3, for compatibility with interpreters
                bw.Write(Application.Version);

                // String entries
                bw.Write(program.stringEntries.Count);
                foreach (KeyValuePair<string, uint> entry in program.stringEntries)
                {
                    bw.Write(entry.Value);
                    WriteString(bw, entry.Key);
                }

                // Value entries
                bw.Write(program.values.Count);
                foreach (KeyValuePair<uint, Value> entry in program.values)
                {
                    bw.Write(entry.Key);
                    WriteValue(bw, entry.Value);
                }

                // Definitions
                bw.Write(program.definitions.Count);
                foreach (KeyValuePair<uint, uint> entry in program.definitions)
                {
                    bw.Write(entry.Key);
                    bw.Write(entry.Value);
                }

                // Command table
                bw.Write(program.commandTable.Count);
                foreach (KeyValuePair<uint, CommandCall> entry in program.commandTable)
                {
                    bw.Write(entry.Key);
                    bw.Write(entry.Value.nameStringID);
                    bw.Write(entry.Value.argValueIDs.Length);
                    foreach (uint arg in entry.Value.argValueIDs)
                    {
                        bw.Write(arg);
                    }
                }

                // Scenes
                bw.Write(program.scenes.Count);
                foreach (KeyValuePair<uint, uint> entry in program.scenes)
                {
                    bw.Write(entry.Key);
                    bw.Write(entry.Value);
                }

                // The actual instructions
                bw.Write(program.instructions.Count);
                foreach (Instruction inst in program.instructions)
                {
                    WriteInstruction(bw, inst);
                }
            }
        }

        void WriteString(LEBinaryWriter bw, string str)
        {
            foreach (char c in str)
                bw.Write(c);
            bw.Write((char)0x00);
        }

        void WriteValue(LEBinaryWriter bw, Value v)
        {
            bw.Write((ushort)v.type);
            switch (v.type)
            {
                case Value.Type.RawIdentifier:
                case Value.Type.String:
                case Value.Type.Variable:
                    bw.Write(v.stringID);
                    break;
                case Value.Type.Int32:
                    bw.Write(v.valueInt32);
                    break;
                case Value.Type.Double:
                    bw.Write(v.valueDouble);
                    break;
                case Value.Type.Boolean:
                    bw.Write(v.valueBoolean);
                    break;
                /*case Value.Type.Array:
                    bw.Write(v.valueArray.Count);
                    foreach (Value val in v.valueArray)
                    {
                        WriteValue(bw, val);
                    }
                    break;*/
                // Undefined has nothing with it
            }
        }

        void WriteInstruction(LEBinaryWriter bw, Instruction inst)
        {
            bw.Write((byte)inst.opcode);
            if (inst.operand1 != null)
                bw.Write((uint)inst.operand1);
            if (inst.operand2 != null)
                bw.Write((uint)inst.operand2);
        }
    }

    class LEBinaryWriter : BinaryWriter
    {
        public LEBinaryWriter(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public override void Write(uint value)
        {
            List<byte> data = new List<byte>(BitConverter.GetBytes(value));
            if (!BitConverter.IsLittleEndian)
                data.Reverse();
            base.Write(data.ToArray());
        }

        public override void Write(int value)
        {
            List<byte> data = new List<byte>(BitConverter.GetBytes(value));
            if (!BitConverter.IsLittleEndian)
                data.Reverse();
            base.Write(data.ToArray());
        }

        public override void Write(ushort value)
        {
            List<byte> data = new List<byte>(BitConverter.GetBytes(value));
            if (!BitConverter.IsLittleEndian)
                data.Reverse();
            base.Write(data.ToArray());
        }

        public override void Write(short value)
        {
            List<byte> data = new List<byte>(BitConverter.GetBytes(value));
            if (!BitConverter.IsLittleEndian)
                data.Reverse();
            base.Write(data.ToArray());
        }

        public override void Write(ulong value)
        {
            List<byte> data = new List<byte>(BitConverter.GetBytes(value));
            if (!BitConverter.IsLittleEndian)
                data.Reverse();
            base.Write(data.ToArray());
        }
        
        public override void Write(long value)
        {
            List<byte> data = new List<byte>(BitConverter.GetBytes(value));
            if (!BitConverter.IsLittleEndian)
                data.Reverse();
            base.Write(data.ToArray());
        }
    }
}
