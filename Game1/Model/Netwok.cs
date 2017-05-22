﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Microsoft.Xna.Framework;
using DerpGame.Controller;
using DerpGame.View;


namespace DerpGame.Model
{
    public class Network
    {
        private TimeSpan lastSync;
        private TimeSpan send;
        public ConcurrentQueue<SPoint> data;
        private ConcurrentBag<IPEndPoint> clients;
        private Controller.DerpGame controller;
        Thread Sever;
        Thread Cli;
       public String request;


		public Network(GameTime last, Controller.DerpGame controller) 
        {
            send = TimeSpan.FromMilliseconds(1);
            lastSync = last.TotalGameTime;
            data = new ConcurrentQueue<SPoint>();
            this.controller = controller;
            request = "i";
            clients = new ConcurrentBag<IPEndPoint>();

        }

        public void ClientSend(UdpClient Client)
        {
                Client.EnableBroadcast = true;
            while (true)
            {
                if (!request.Equals("null")&& !request.Contains("i"))
                {
                    var RequestData = Encoding.ASCII.GetBytes(request);
                    Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 8888));
                    request = "null";
                }
                Thread.Sleep(20);
            }

        }
        private void ClientRecive(UdpClient Client)
        {
            var ServerEp = new IPEndPoint(IPAddress.Any, 0);
            Client.EnableBroadcast = true;
            var RequestData = Encoding.ASCII.GetBytes(request);
            Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 8888));
            request = "null";
            while (true)
            {
               
                var ServerResponseData = Client.Receive(ref ServerEp);
                var ServerResponse = ByteArrayToObject(ServerResponseData);
                List<SPoint> reciveDdata = (List<SPoint>)ServerResponse;

                foreach (SPoint point in reciveDdata)
                {
                    data.Enqueue(point);
                }
            }
        }

        private void killSever()
        {
            if (Sever == null || !Sever.IsAlive)
            {
                UdpClient Server = new UdpClient(8888);
                Thread Recieve = new Thread(() => ServerRecive(Server));
                Recieve.Start();
                Sever = new Thread(() => SeverSend(Server));
                Sever.Start();
                ThreadStart kill = new ThreadStart(killNoise);
                Thread noise = new Thread(kill);
                noise.Start();
            }
        }
        private void killClient()
        {
            if (Cli == null || !Cli.IsAlive)
            {
                UdpClient Client = new UdpClient();
                Cli = new Thread(()=>ClientRecive(Client));
                Cli.Start();
                Thread send = new Thread(() => ClientSend(Client));
                send.Start();
            }
        }
        public void killNoise()
        {
            while (true)
            {
                SPoint point;
                if (data.Count > 5000)
                {
                    while (!data.IsEmpty)
                    {
                        data.TryDequeue(out point);
                    }
                }
                Thread.Sleep(100);

            }
        }
        public void StartClient(GameTime time)
        {
            killClient();
        }
        public void StartSever(GameTime time)
        {
            killSever();
            
        }
        private void ServerRecive(UdpClient Server)
        {
                var ClientEp = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    var ClientRequestData = Server.Receive(ref ClientEp);
                    String request = Encoding.ASCII.GetString(ClientRequestData);
                    if (request.Contains("i"))
                    {
                        Console.Write("add");
                        Player player = new Player();
                        Animation animation = new Animation();
                        animation.Initialize(controller.playerTexture, Vector2.Zero, 115, 69, 8, 30, Color.White, 1f, true);
                        Vector2 playerPosition = new Vector2(controller.GraphicsDevice.Viewport.TitleSafeArea.X, controller.GraphicsDevice.Viewport.TitleSafeArea.Y
                        + controller.GraphicsDevice.Viewport.TitleSafeArea.Height / 2);
                        player.Initialize(animation, playerPosition, ClientEp.Address.ToString());
                        controller.players.Add(player);
                        clients.Add(ClientEp);

                    }
                    else
                    {

                        Player player = new Player();
                        foreach (Player currentPlayer in controller.players)
                        {
                            if (currentPlayer != null && currentPlayer.Id.Equals(ClientEp.Address.ToString()))
                            {
                                player = currentPlayer;
                            }
                        }

                        // Use the Keyboard / Dpad
                        if (request.Contains("a"))
                        {
                            player.Position.X -= player.MovementSpeed;
                        }
                        if (request.Contains("d"))
                        {
                            player.Position.X += player.MovementSpeed;
                        }
                        if (request.Contains("w"))
                        {
                            player.Position.Y -= player.MovementSpeed;
                        }
                        if (request.Contains("s"))
                        {
                            player.Position.Y += player.MovementSpeed;
                        }
                        if (request.Contains(" "))
                        {
                            player.spacePressed = true;
                        }
                    }
                }
                catch (SocketException e)
                {
                    List<IPEndPoint> safe = new List<IPEndPoint>();
                    IPEndPoint Out;
                    while (!clients.IsEmpty)
                    {
                        clients.TryTake(out Out);
                        if (Out != null && !Out.Address.ToString().Equals(ClientEp.Address.ToString()))
                        {
                            safe.Add(Out);
                        }
                            }
                    foreach (IPEndPoint CLient in safe)
                    {
                        clients.Add(CLient);
                    }
                    Player POut;
                    List<Player> Psafe = new List<Player>();
                    while (!controller.players.IsEmpty)
                    {
                        controller.players.TryTake(out POut);
                        if (POut != null && !POut.Id.Equals(ClientEp.Address.ToString()))
                        {
                            Console.WriteLine(POut.Id);
                            Psafe.Add(POut);
                        }
                    }
                    foreach (Player CLient in Psafe)
                    {
                        controller.players.Add(CLient);
                    }
                    Console.WriteLine(ClientEp.Address.ToString());
                    Console.WriteLine(controller.players.Count);
                }
            }

            

        }

        private void SeverSend(UdpClient Server)
        {
            while (true)
            {
                List<SPoint> toSerialize = new List<SPoint>();
                for (int index = 0; index < data.Count; index++)
                {
                    SPoint outPoint;
                    data.TryDequeue(out outPoint);
                    if (outPoint == null)
                    {
                        Thread.Sleep(1);
                        data.TryDequeue(out outPoint);
                    }
                    toSerialize.Add(outPoint);
                }
                byte[] ResponseData = ObjectToByteArray(toSerialize);
                try
                {
                    foreach (IPEndPoint Client in clients)
                    {
                        Server.Send(ResponseData, ResponseData.Length, Client);
                    }
                }
                catch
                {
                    SPoint point;
                    while (!data.IsEmpty)
                    {
                        data.TryDequeue(out point);
                    }
                    byte[] blank = ObjectToByteArray(new List<SPoint>());
                    foreach (IPEndPoint Client in clients)
                    {
                        Server.Send(blank, blank.Length, Client);
                    }
                }
                Thread.Sleep(30);
            }
        }
        
            private byte[] ObjectToByteArray(Object obj)
		{
			if (obj == null)
				return null;

			BinaryFormatter bf = new BinaryFormatter();
			MemoryStream ms = new MemoryStream();
			bf.Serialize(ms, obj);

			return ms.ToArray();
		}

		// Convert a byte array to an Object
		private Object ByteArrayToObject(byte[] arrBytes)
		{
			MemoryStream memStream = new MemoryStream();
			BinaryFormatter binForm = new BinaryFormatter();
			memStream.Write(arrBytes, 0, arrBytes.Length);
			memStream.Seek(0, SeekOrigin.Begin);
			Object obj = (Object)binForm.Deserialize(memStream);

			return obj;
		}
    }
}
