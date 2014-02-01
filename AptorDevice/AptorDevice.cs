/**
 * AptorDevice.cs - Behaviour of the Aptor Device
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Aptor
{
    public class AptorDevice : PartModule
    {
        private struct ascentpoint
        {
            double time;
            double pitch;
            double thrust;
        } ;
        private List<ascentpoint> ascent;

        private static int _globalId = 0;
        private static int globalId { get { return _globalId++; } }
        private const int windowId = 915384;
        private const int drawQueueNumber = 732983;
        private Rect windowPos = new Rect(Screen.width / 4, Screen.height / 8, 500, 100);
        private int idd = globalId;
        private bool _isPrimaryAptorDevice = false;
        public bool isPrimaryAptorDevice { get { return _isPrimaryAptorDevice; } }
        private bool _isAttached = false;
        public bool isAttached { get { return _isAttached; } }

        private volatile bool joinMe = false;
        private volatile bool socketInAction = false;
        private volatile bool stopSocketAction = false;
        private volatile bool computationFinished = false;
        private volatile string errorString = "";
        private volatile string logging = "";

        private int targetPort = 55556;
        private byte[] targetIp = new byte[4] { 127, 0, 0, 1 };

        private Thread socketWorkerThread;

        /* LOGGING ***************************************************************/

        private void log(string res)
        {
            Debug.Log("AD " + idd + ": " + res);
        }
        private void logEr(string res)
        {
            Debug.LogError("AD " + idd + ": " + res);
        }

        /* Primary device status ***************************************************************/

        public override void OnStart(StartState state)
        {
            //log("onStart");
            if (state == StartState.Editor)
            {
                part.OnEditorAttach = onEditorAttachPart;
                part.OnEditorDetach = onEditorDetachPart;
                part.OnEditorDestroy = onEditorDestroyPart;
                print("AD OnStart PI in editor");
                if (part.parent != null)
                {
                    onEditorAttachPart();
                }
            }
            else
            {
                //log("OnStart PI not in editor");
            }
        }
        public override void OnUpdate() // called every frame while in Flightmode
        {
        }
        private void onEditorAttachPart()
        {
            //log("onEditorAttachPart");
            _isAttached = true;
            if (!_isPrimaryAptorDevice)
            {
                updateGlobalPrimaryAptorDeviceStatus();
            }
        }

        private void onEditorDetachPart()
        {
            //log("onEditorDetachPart");
            _isAttached = false;
            if (_isPrimaryAptorDevice)
            {
                updateGlobalPrimaryAptorDeviceStatus();
            }
        }
        private void onEditorDestroyPart()
        {
            //log("onEditorDestroyPart");
            _isAttached = false;
            if (_isPrimaryAptorDevice)
            {
                updateGlobalPrimaryAptorDeviceStatus();
            }
        }

        private void updateGlobalPrimaryAptorDeviceStatus()
        {
            //log("updateGlobalPrimaryAptorDeviceStatus");
            AptorDevice ad = getFirstAttachedAptorDevice();
            if (ad == null) // no device attached
            {
                if (_isPrimaryAptorDevice)
                {
                    _isPrimaryAptorDevice = false;
                    OnAptorDeactivation();
                }
            }
            else if (ad == this) // this is first
            {
                if (!_isPrimaryAptorDevice)
                {
                    _isPrimaryAptorDevice = true;
                    ad = getFirstAttachedAptorDeviceThatIsNotMe();
                    if (ad != null)
                    {
                        ad.updateLocalPrimaryAptorDeviceStatus();
                    }
                    OnAptorActivation();
                }
            }
            else // this is not first
            {
                if (_isPrimaryAptorDevice)
                {
                    _isPrimaryAptorDevice = false;
                    OnAptorDeactivation();
                    if (! ad.isPrimaryAptorDevice)
                    {
                        ad.updateLocalPrimaryAptorDeviceStatus();
                    }
                }
            }
        }

        public void updateLocalPrimaryAptorDeviceStatus()
        {
            AptorDevice ad = getFirstAttachedAptorDevice();
            if (ad == this)
            {
                if (!_isPrimaryAptorDevice)
                {
                    _isPrimaryAptorDevice = true;
                    OnAptorActivation();
                }
            }
            else
            {
                if (_isPrimaryAptorDevice)
                {
                    _isPrimaryAptorDevice = false;
                    OnAptorDeactivation();
                }
            }
        }

        private AptorDevice getFirstAttachedAptorDeviceThatIsNotMe()
        {
            List<Part> pl = Utility.getPartList();

            if (pl.Count == 0)
            {
                return null;
            }

            Part pa = pl.Where(i => i.Modules.Contains("AptorDevice") && i != part && i.Modules.OfType<AptorDevice>().First().isAttached).FirstOrDefault();
            return getFirstAptorDeviceOfMaybeNullPart(pa);
        }

        private AptorDevice getFirstAttachedAptorDevice()
        {
            List<Part> pl = Utility.getPartList();

            if (pl.Count == 0)
            {
                return null;
            }

            Part pa = pl.Where(i => i.Modules.Contains("AptorDevice") && i.Modules.OfType<AptorDevice>().First().isAttached).FirstOrDefault();
            return getFirstAptorDeviceOfMaybeNullPart(pa);
        }

        private AptorDevice getFirstAptorDeviceOfMaybeNullPart(Part part)
        {
            if (part == null)
            {
                return null;
            }
            else
            {
                return part.Modules.OfType<AptorDevice>().First();
            }

        }

        private void OnAptorActivation()
        {
            //log("gained primary status");
            ActivateWindow();
        }
        private void OnAptorDeactivation()
        {
            //log("lost primary status");
            DeactivateWindow();
        }

        /* Window display ***************************************************************/

        private void ActivateWindow()
        {
            RenderingManager.AddToPostDrawQueue(drawQueueNumber, getSceneShowWindowCallback());
        }
        private void DeactivateWindow()
        {
            RenderingManager.RemoveFromPostDrawQueue(drawQueueNumber, getSceneShowWindowCallback());
        }

        private Callback getSceneShowWindowCallback()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return new Callback(showEditorWindow);
            }
            else
            {
                throw new NotImplementedException("AD else branch of getSceneShowWindowCallback");

            }
        }
        private void showEditorWindow()
        {
            if (logging != "")
            {
                Debug.Log(logging);
                logging = "";
            }
            //Debug.Log("AD in show window");
            GUI.skin = HighLogic.Skin;
            windowPos = GUILayout.Window(windowId, windowPos, CreateEditorWindowContents, "Stage Infos");
        }
        private void CreateEditorWindowContents(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("I am " + idd);
            if (GUILayout.Button("Send Socket Message",GUILayout.ExpandWidth(false)))
            {
                RocketSpec rs = new RocketSpec();
                log("Pres Send Socket");
                PerformAscentCalculation(rs);
                log("Send Socket");
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
        private void PerformAscentCalculation(RocketSpec rs)
        {
            List<string> socketCommands = new List<string>();
            // Planet Spec
            socketCommands.Add("PLANET_MASS 5.2915793E22");
            socketCommands.Add("PLANET_RADIUS 600000");
            socketCommands.Add("PLANET_SCALE_HEIGHT 5000");
            socketCommands.Add("PLANET_P0 1");
            socketCommands.Add("PLANET_ROTATION_PERIOD 21600");
            socketCommands.Add("PLANET_SOI 84159286");
            // Rocket Spec
            RocketSpec.Rocket r = rs.getRocketSpec();
            for (int s = r.stages.Count - 1; s >= 0; s--)
            {
                if (r.engines.Count > 0)
                {
                    socketCommands.Add("ADD_STAGE " + r.stages[s].initialMass + " " + r.stages[s].fuelMass + " " + r.stages[s].drag);
                    foreach (RocketSpec.Engine e in r.engines)
                    {
                        if (e.ignitionStage >= s && e.burnoutStage <= s)
                        {
                            socketCommands.Add("ADD_ENGINE " + e.thrust + " " + e.isp0 + " " + e.ispV);
                        }
                    }
                }
            }
            // Config
            socketCommands.Add("LAUNCH_LATITUDE -0.001691999747072392");
            socketCommands.Add("LAUNCH_LONGITUDE 0");
            socketCommands.Add("LAUNCH_ALTITUDE 77.6");
            socketCommands.Add("MAX_VELOCITY 10000");
            socketCommands.Add("NAME |" + EditorLogic.fetch.shipNameField.Text);
            socketCommands.Add("TARGET_PERIAPSIS 75000");
            socketCommands.Add("ITERATIONS 40");
            socketCommands.Add("SET_NODES 9 18");
            socketCommands.Add("MESH_REFINEMENT manual");
            socketCommands.Add("NLP_TOLERANCE 1.0e-5");
            socketCommands.Add("COMPUTE");
            socketCommands.Add("POSTPROCESS");
            socketCommands.Add("GET_PITCH_THRUST 0");

            string res = "";
            foreach (string s in socketCommands)
            {
                res += s + "\n";
            }
            
            log(res);

            if (joinMe)
            {
                socketWorkerThread.Join();
                joinMe = false;
            }
            if (!socketInAction)
            {
                socketInAction = true;
                // I perform the socket operation
                SocketTalker st = new SocketTalker();
                st.ad = this;
                st.socketCommands = socketCommands;
                ThreadStart ts = new ThreadStart(st.doSocketWork);
                socketWorkerThread = new Thread(ts);
                socketWorkerThread.Start();
                while (!socketWorkerThread.IsAlive) ; 
                log("post start");
            }
            else
            {
                log("server already running");
            }

        }

        public class SocketTalker
        {
            public List<String> socketCommands;
            Socket localSocket;
            public AptorDevice ad;
            IPAddress ipa;
            IPEndPoint targetEndpoint;
            IPEndPoint localEndpoint;
    
            private void log(string res)
            {
                ad.logging += "\nADST: " + res;
            }
            public SocketTalker()
            {
                Debug.Log("AAAA create socket talker");

            }
            public void doSocketWork()
            {
                ipa = new IPAddress(ad.targetIp);
                targetEndpoint = new IPEndPoint(ipa, ad.targetPort);
                localEndpoint = new IPEndPoint(ipa, ad.targetPort+1);
                log("init socket " + ad.targetIp + ":" + ad.targetPort);
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
                        bool endThis = false;
                        log("Connection OK");
                        foreach (string c in socketCommands)
                        {
                            log("sending ..." + c);
                            byte[] buffer = Encoding.ASCII.GetBytes(c);
                            try
                            {
                                localSocket.Send(buffer, c.Length, SocketFlags.None);
                            }
                            catch (ArgumentNullException e)
                            {
                                log("ArgumentNullException " + e.Message);
                            }
                            catch (ArgumentOutOfRangeException e)
                            {
                                log("ArgumentOutOfRangeException " + e.Message);
                            }
                            catch (SocketException e)
                            {
                                log("SocketException " + e.ErrorCode);
                            }
                            catch (ObjectDisposedException e)
                            {
                                endThis = true;
                                log("ObjectDisposedException " + e.Message);
                            }
                            log("sending done ... receiving");
                            byte[] rcvBuffer = new byte[4096];
                            int len;
                            try
                            {
                                len = localSocket.Receive(rcvBuffer, 4096 ,SocketFlags.None);
                                string res = System.Text.Encoding.ASCII.GetString(rcvBuffer, 0, len);
                                log("Received: \"" + res + "\"");
                            }
                            catch (ArgumentNullException e)
                            {
                                log("ArgumentNullException " + e.Message);
                            }
                            catch (SocketException e)
                            {
                                log("SocketException " + e.ErrorCode);
                            }
                            catch (ObjectDisposedException e)
                            {
                                log("ObjectDisposedException (socket closed) " + e.Message);
                                endThis = true;
                            }
                            catch (System.Security.SecurityException e)
                            {
                                log("SecurityException" + e.Message);
                            }
                            log("receiving done");

                            if (endThis)
                            {
                                log("beakting");
                                break;
                            }
                            else
                            {
                                if (ad.stopSocketAction)
                                {
                                    log("beakting");
                                    break;
                                }
                            }
                        }
                        localSocket.Shutdown(SocketShutdown.Both);
                    }
                    else
                    {
                        log("Unable to connect to Server");
                    }
                    localSocket.Close();

                }
                log("Shutting down");
                ad.socketInAction = false;
                ad.joinMe = true;
            }

            private void send(string c)
            {
                log("sending " + c);

            }

            private void initSocket()
            {
            }
        } // end class SocketTalker

    } // end class AptorDevice
}
