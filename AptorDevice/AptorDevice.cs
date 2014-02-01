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
    public partial class AptorDevice : PartModule
    {
        private enum ComputationStatus
        {
            PreInit,
            Starting,
            DoingComputation,
            ComputationFinished,
            GettingTimes,
            GettingResults,
            Waiting
        }
        private ComputationStatus computationStatus = ComputationStatus.PreInit;
        private enum FlightStatus
        {
            PreStart,
            RetrievingAscentData,
            Launchpad,
            InFlight,
            PostAscent
        }
        private FlightStatus flightStatus;
        public struct ascentpoint
        {
            public double time;
            public double pitch;
            public double thrust;
            public double posX;
            public double posY;
            public double posZ;
            public double velX;
            public double velY;
            public double velZ;
            public double thrX;
            public double thrY;
            public double thrZ;
            public double mass;
        } ;
        private List<ascentpoint> ascent = new List<ascentpoint>();

        private static int _globalId = 0;
        private static int globalId { get { return _globalId++; } }
        private const int windowId = 915384; // hopefully unique
        private const int drawQueueNumber = 732983; // hopefully unique
        private Rect windowPos = new Rect(Screen.width / 4, Screen.height / 8, 500, 100);
        private int idd = globalId;
        private bool _isPrimaryAptorDevice = false;
        public bool isPrimaryAptorDevice { get { return _isPrimaryAptorDevice; } }
        private bool _isAttached = false;
        public bool isAttached { get { return _isAttached; } }

        private SocketTalker socketTalker = null;
        private Thread socketWorkerThread = null;

        private string targetPortString = "55556";
        private byte[] targetIp = new byte[4] { 127, 0, 0, 1 };
        private byte[] localIp = new byte[4] { 127, 0, 0, 1 };
        private int localPort = 55557;
        private string iterations = "40";
        private string nodes = "10 20";


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
                OnEditorStart();
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                log("OnStart In Flight");
                part.OnJustAboutToBeDestroyed = onFlightDestroyPart;
                _isAttached = true;

                updateGlobalPrimaryAptorDeviceStatus();
                log("State: " + state.ToString());
                if (state == (StartState.PreLaunch | StartState.Landed)) {
                    flightStatus = FlightStatus.PreStart;
                } else {
                    flightStatus = FlightStatus.PostAscent;
                }
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

        private void onFlightDestroyPart()
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
            log("updateGlobalPrimaryAptorDeviceStatus");
            AptorDevice ad = getFirstAttachedAptorDevice();
            if (ad == null) // no device attached
            {
                log("no decive attached");
                if (_isPrimaryAptorDevice)
                {
                    _isPrimaryAptorDevice = false;
                    OnAptorDeactivation();
                }
            }
            else if (ad == this) // this is first
            {
                log("this is first");
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
                log("this is not first");
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
            List<Part> pl = getPartList();

            if (pl.Count == 0)
            {
                return null;
            }

            Part pa = pl.Where(i => i.Modules.Contains("AptorDevice") && i != part && i.Modules.OfType<AptorDevice>().First().isAttached).FirstOrDefault();
            return getFirstAptorDeviceOfMaybeNullPart(pa);
        }

        private AptorDevice getFirstAttachedAptorDevice()
        {
            List<Part> pl = getPartList();

            if (pl.Count == 0)
            {
                log("vessel part list empty");
                return null;
            }
            log("vessel parts: " + pl.Count);
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
            log("gained primary status");
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
            else if (HighLogic.LoadedSceneIsFlight)
            {
                return new Callback(showFlightWindow);
            }
            else
            {
                throw new NotImplementedException("AD else branch of getSceneShowWindowCallback");

            }
        }

        private void showFlightWindow()
        {
            //Debug.Log("AD in show flight window");
            GUI.skin = HighLogic.Skin;
            windowPos = GUILayout.Window(windowId, windowPos, CreateFlightWindowContents, "Aptor");

        }
        private void CreateFlightWindowContents(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("I am " + idd + " in " + flightStatus.ToString());

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Port", GUILayout.ExpandWidth(false));
                targetPortString = GUILayout.TextField(targetPortString, 5, GUILayout.ExpandWidth(false));

            }
            GUILayout.EndHorizontal();
            
            if (flightStatus == FlightStatus.PreStart)
            {
                if (GUILayout.Button("Retreive Ascent Data", GUILayout.ExpandWidth(false)))
                {
                    log("retrieve");
                }

            }
            else if (flightStatus == FlightStatus.RetrievingAscentData)
            {
                GUILayout.Label("Retreiving Ascent Data ...", GUILayout.ExpandWidth(false));
            }
            else if (flightStatus == FlightStatus.Launchpad)
            {
                GUILayout.Label("Ready for Liftoff", GUILayout.ExpandWidth(false));
            }
            else if (flightStatus == FlightStatus.InFlight)
            {
                GUILayout.Label("During Ascent", GUILayout.ExpandWidth(false));
            }
            else if (flightStatus == FlightStatus.PostAscent)
            {
                GUILayout.Label("Ascent Done", GUILayout.ExpandWidth(false));
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
        private void onTick()
        {
            updateAscentCalculationStatus();
            updateThreadStatus();
        }

        private void updateThreadStatus()
        {
            if (socketWorkerThread != null)
            {
                if (socketTalker.finished)
                {
                    socketWorkerThread.Join();
                    socketWorkerThread = null;
                    socketTalker = null;
                }
            }
        }

        private void updateAscentCalculationStatus()
        {
            if (socketTalker == null)
            {
                return;
            }
            if (socketTalker.inputQueueEmpty)
            {
                if (computationStatus == ComputationStatus.DoingComputation)
                {
                    socketTalker.clearResponseQueue();
                    computationStatus = ComputationStatus.GettingTimes;
                    socketTalker.addCommand("GET_FINAL_TIMES");
                }
                else if (computationStatus == ComputationStatus.GettingTimes)
                {
                    computationStatus = ComputationStatus.GettingResults;
                    string times = socketTalker.getNextAnswer();
                    List<string> tim = times.Split(' ').ToList();
                    int total = Convert.ToInt32(Math.Floor(Convert.ToDouble(tim[tim.Count - 1])));
                    log("total = " + total);
                    for (int t = 0; t <= total; t++)
                    {
                        socketTalker.addCommand("GET_PITCH_THRUST " + t);
                    }
                }
                else if (computationStatus == ComputationStatus.GettingResults) {
                    computationStatus = ComputationStatus.ComputationFinished;
                    bool getRes = true;
                    while (getRes)
                    {
                        string ans = socketTalker.getNextAnswer();
                        if (ans == null) {
                            getRes = false;
                        }
                        else
                        {
                            List<string> pl = ans.Split(' ').ToList();
                            log("in pt " + pl[0] + " " + pl[1] + " " + pl[2] + " (" + ans + ")\n");
                            ascentpoint aP = new ascentpoint();
                            aP.time = Convert.ToDouble(pl[0]);
                            aP.thrust = Convert.ToDouble(pl[1]);
                            aP.pitch = Convert.ToDouble(pl[2]);
                            ascent.Add(aP);
                        }
                    }
                    socketTalker.doShutdown = true;
                }
            }
        }

        private bool duringAscentCalculation()
        {
            return (computationStatus == ComputationStatus.DoingComputation ||
                computationStatus == ComputationStatus.GettingResults);
        }

        private bool canDoAscentCalculation()
        {
            return (computationStatus == ComputationStatus.PreInit ||
                computationStatus == ComputationStatus.Starting ||
                computationStatus == ComputationStatus.Waiting ||
                computationStatus == ComputationStatus.ComputationFinished);
        }
        private void PerformAscentCalculation(RocketSpec rs)
        {
            if (duringAscentCalculation()) {
                Debug.LogError("Cannot perform ascent calculation during ascent calculation");
                return;
            }
            initThread();

            // Planet Spec
            socketTalker.addCommand("PLANET_MASS 5.2915793E22");
            socketTalker.addCommand("PLANET_RADIUS 600000");
            socketTalker.addCommand("PLANET_SCALE_HEIGHT 5000");
            socketTalker.addCommand("PLANET_P0 1");
            socketTalker.addCommand("PLANET_ROTATION_PERIOD 21600");
            socketTalker.addCommand("PLANET_SOI 84159286");
            // Rocket Spec
            RocketSpec.Rocket r = rs.getRocketSpec();
            for (int s = r.stages.Count - 1; s >= 0; s--)
            {
                bool first = true;
                foreach (RocketSpec.Engine e in r.engines)
                {
                    if (e.ignitionStage >= s && e.burnoutStage <= s)
                    {
                        if (first)
                        {
                            first = false;
                            socketTalker.addCommand("ADD_STAGE " + r.stages[s].initialMass + " " + r.stages[s].fuelMass + " " + r.stages[s].drag);
                        }
                        socketTalker.addCommand("ADD_ENGINE " + e.thrust + " " + e.isp0 + " " + e.ispV);
                    }
                }
            }
            // Config
            socketTalker.addCommand("LAUNCH_LATITUDE -0.001691999747072392");
            socketTalker.addCommand("LAUNCH_LONGITUDE 0");
            socketTalker.addCommand("LAUNCH_ALTITUDE 77.6");
            socketTalker.addCommand("MAX_VELOCITY 10000");
            socketTalker.addCommand("NAME " + EditorLogic.fetch.shipNameField.Text);
            socketTalker.addCommand("TARGET_PERIAPSIS 75000");
            socketTalker.addCommand("ITERATIONS " + iterations);
            socketTalker.addCommand("SET_NODES " + nodes);
            socketTalker.addCommand("MESH_REFINEMENT manual");
            socketTalker.addCommand("NLP_TOLERANCE 1.0e-5");
            socketTalker.addCommand("COMPUTE");
            socketTalker.addCommand("POSTPROCESS");
            computationStatus = ComputationStatus.DoingComputation;
        }

        private void initThread()
        {
            if (socketWorkerThread == null)
            {
                if (socketTalker == null)
                {
                    socketTalker = new SocketTalker();
                    socketTalker.targetIp = new IPAddress(targetIp);
                    socketTalker.targetPort = Convert.ToInt32(targetPortString);
                    socketTalker.localIp = new IPAddress(localIp);
                    socketTalker.localPort = localPort;
                }
                ThreadStart ts = new ThreadStart(socketTalker.doSocketWork);
                socketWorkerThread = new Thread(ts);
                socketWorkerThread.Start();
                while (!socketWorkerThread.IsAlive) ;
            }

        }
        private List<Part> getPartList()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                log("get Part List is in editor");
                return EditorLogic.SortedShipList;
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                log("get Part List is in flight");
                return vessel.parts;
            }
            else
            {
                throw new NotImplementedException("AD else branch of getPartList");
            }
        }

        private Part getRoot()
        {
            return getPartList()[0];
        }

    } // end class AptorDevice
}
