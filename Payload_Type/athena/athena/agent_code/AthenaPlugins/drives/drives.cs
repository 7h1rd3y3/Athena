using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Athena.Plugins;

namespace Plugins
{
    public class Drives : AthenaPlugin
    {
        public override string Name => "drives";
        public override void Execute(Dictionary<string, string> args)
        {
            StringBuilder output = new StringBuilder();
            output.Append("[");
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                try
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{d.DriveType}\",\"FreeSpace\":\"{d.TotalFreeSpace.ToString()}\",\"TotalSpace\":\"{d.TotalSize.ToString()}\"}},");
                }
                catch (IOException e)
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{e.Message.Split(":")[0].TrimEnd(' ')}\",\"FreeSpace\":\"\",\"TotalSpace\":\"\"}},");
                }
                catch (Exception e)
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{e.Message.Split(":")[0].TrimEnd(' ')}\",\"FreeSpace\":\"\",\"TotalSpace\":\"\"}},");

                    output.Remove(output.Length - 1, 1);
                    output.Append("]");
                }
            }
            output.Remove(output.Length - 1, 1);
            output.Append("]");

            PluginHandler.Write(output.ToString(), args["task-id"], true);
        }
    }
}