/**
 * SocketTalker.cs - Socket Interface Aptor Device
 *
 * This file is part of Aptor.
 *
 * Copyright 2014 Mhoram Kerbin
 *
 * Aptor is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Aptor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Aptor. If not, see <http://www.gnu.org/licenses/>.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Aptor
{
    class SocketTalker
    {
        private volatile Queue<string> socketCommands = new Queue<string>();
        private volatile Queue<string> socketResponse = new Queue<string>();
        public volatile IPAddress targetIp;
        public volatile int targetPort;
        public volatile IPAddress localIp;
        public volatile int localPort;
        private IPEndPoint targetEndpoint;
        private IPEndPoint localEndpoint;
        public volatile bool doShutdown = false;
        private Socket localSocket;
        public volatile bool inputQueueEmpty;
        public volatile bool finished = false;

        public void addCommand(string command)
        {
            inputQueueEmpty = false;
            socketCommands.Enqueue(command);
        }

        public string getNextAnswer()
        {
            if (socketResponse.Count == 0)
            {
                return null;
            }
            else
            {
                return socketResponse.Dequeue();
            }
        }

        private void log(string res)
        {
            Debug.Log("ADST: " + res);
        }
        public SocketTalker()
        {
            log("AAAA create socket talker");
        }
        public void doSocketWork()
        {
            inputQueueEmpty = false;
            targetEndpoint = new IPEndPoint(targetIp, targetPort);
            localEndpoint = new IPEndPoint(localIp, localPort);
            log("init socket " + targetIp + ":" + targetPort);
            localSocket = null;
            try
            {
                localSocket = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException e)
            {
                log("Error during socket creation: " + e.ErrorCode);
            }
            log("No Error during socket creation");

            if (localSocket != null)
            {
                log("pre connect");
                try
                {
                    localSocket.Connect(targetEndpoint);
                }
                catch (ArgumentNullException e)
                {
                    log("ArgumentNullException " + e.Message);
                }
                catch (SocketException e)
                {
                    log("Socket Exception" + e.ErrorCode);
                }
                catch (ObjectDisposedException e)
                {
                    log("ObjectDisposedException" + e.Message);
                }
                catch (System.Security.SecurityException e)
                {
                    log("SecurityException" + e.Message);
                }
                catch (InvalidOperationException e)
                {
                    log("InvalidOperationException (socket is listening) " + e.Message);
                }
                log("post connect");

                if (localSocket.Connected)
                {
                    log("Connection OK");
                    while (!doShutdown)
                    {
                        if (socketCommands.Count == 0)
                        {
                            inputQueueEmpty = true;
                            System.Threading.Thread.Sleep(1000); // TODO: replace by trigger
                        }
                        else
                        {
                            try
                            {
                                string c = socketCommands.Dequeue();
                                socketResponse.Enqueue(send(c));
                            }
                            catch (ObjectDisposedException e)
                            {
                                doShutdown = true;
                                log("S ObjectDisposedException " + e.Message);
                            }
                        }

                    }
                }
                else
                {
                    log("Unable to connect to Server");
                }
                localSocket.Close();

            }
            log("Shutting down");
            finished = true;
        }

        private string send(string c)
        {
            string res = "";
            log("sending ..." + c);
            byte[] buffer = Encoding.ASCII.GetBytes(c);
            try
            {
                localSocket.Send(buffer, c.Length, SocketFlags.None);
            }
            catch (ArgumentNullException e)
            {
                log("S ArgumentNullEx;ception " + e.Message);
            }
            catch (ArgumentOutOfRangeException e)
            {
                log("S ArgumentOutOfRangeException " + e.Message);
            }
            catch (SocketException e)
            {
                log("S SocketException " + e.ErrorCode);
            }
            log("sending done ... receiving");
            byte[] rcvBuffer = new byte[4096];
            int len;
            try
            {
                len = localSocket.Receive(rcvBuffer, 4096, SocketFlags.None);
                res = System.Text.Encoding.ASCII.GetString(rcvBuffer, 0, len);
                log("Received: \"" + res + "\"");
            }
            catch (ArgumentNullException e)
            {
                log("R ArgumentNullException " + e.Message);
            }
            catch (SocketException e)
            {
                log("R SocketException " + e.ErrorCode);
            }
            catch (System.Security.SecurityException e)
            {
                log("R SecurityException" + e.Message);
            }
            log("receiving done");
            return res;
        }

        private void initSocket()
        {
        }

        public void clearResponseQueue()
        {
            socketResponse.Clear();
        }
    }
}
