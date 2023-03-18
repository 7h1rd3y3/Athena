using System;
using System.Collections.Generic;
using System.IO;
using PluginBase;

namespace Plugin
{
    public static class cd
    {

        public static void Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path") && !string.IsNullOrEmpty((string)args["path"]))
                {
                    string path = ((string)args["path"]).Replace("\"", "");

                    Directory.SetCurrentDirectory(path);

                    PluginHandler.Write($"Changed directory to {Directory.GetCurrentDirectory()}", (string)args["task-id"], true);
                }
                else
                {
                    PluginHandler.Write("Missing path parameter", (string)args["task-id"], true, "error");
                }
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
    }
}
