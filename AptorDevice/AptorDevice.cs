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
using System.Text;
using UnityEngine;

namespace Aptor
{
    public class AptorDevice : PartModule
    {
        private const int windowId = 912378;
        private const int drawQueueNumber = 732983;
        private bool windowViewable = false;
        private Rect windowPos = new Rect(Screen.width / 4, Screen.height / 8, 500, 100);
        private float idd = UnityEngine.Random.value;
        private bool isPrimaryAptorDevice = false;
        private bool isInShutdown = false;
        private bool doUpdatePrimaryStatusAtNextFrame = true;

        public bool isMaster()
        {
            return isPrimaryAptorDevice;
        }
        public bool isShuttingDown()
        {
            return isInShutdown;
        }
        public override void OnStart(StartState state)
        {
            print("AD onStart");
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
                print("AD OnStart PI not in editor");
            }
        }
        public override void OnUpdate() // called every frame while in Flightmode
        {
        }
        private void onEditorAttachPart()
        {
            Debug.Log("AD onEditorAttachPart");
            updatePrimaryAptorDeviceStatus();
        }

        private void onEditorDetachPart()
        {
            Debug.Log("AD onEditorDetachPart");
            disablePrimaryDeviceStatus();
 
        }
        private void onEditorDestroyPart()
        {
            Debug.Log("AD onEditorDestroyPart");
            disablePrimaryDeviceStatus();
        }

        public void updatePrimaryAptorDeviceStatus()
        {
            print("AD UpdatePrimaryAptorDeviceStatus");
            Part fap = getFirstAptorPart();
            print("AD past gfap");
            if (fap == null)
            {
                Debug.Log("AD no Aptor Part available");
                disablePrimaryDeviceStatus();
                return;
            }
            if (fap == part)
            {
                Debug.Log("AD I am Master " + idd);
                enablePrimaryDeviceStatus();
            }
            else
            {
                Debug.Log("AD I am Slave " + idd);
                disablePrimaryDeviceStatus();
            }
        }

        private Part getFirstAptorPart()
        {
            List<Part> pl = getPartList();

            if (pl.Count == 0)
            {
                return null;
            }
            return pl.Where(i => i.Modules.Contains("AptorDevice")).FirstOrDefault();
        }

        public void updatePrimaryStatusNow()
        {
            if (!isInShutdown)
            {
                updatePrimaryAptorDeviceStatus();
            }
        }

        private Part getFirstAptorPartThatIsNotMe()
        {
            Debug.Log("AD in getFirstAptorPartThatIsNotMe");

            List<Part> pl = getPartList();
            if (pl.Count == 0)
            {
                Debug.Log("AD no other AP");
                return null;
            }
            Debug.Log("AD other AP found");
            return pl.Where(i => i.Modules.Contains("AptorDevice") && i != part).FirstOrDefault();
        }

        private List<Part> getPartList()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return EditorLogic.SortedShipList;
            }
            else
            {
                throw new NotImplementedException("AD else branch of getPartList");
            }
        }

        private void disablePrimaryDeviceStatus()
        {
            print("AD disablePrimaryDeviceStatus");

            if (isPrimaryAptorDevice)
            {
                OnAptorDeactivation();
            }
        }
        private void enablePrimaryDeviceStatus()
        {
            print("AD enablePrimaryDeviceStatus");
            if (! isPrimaryAptorDevice)
            {
                OnAptorActivation();
            }
        }

        private void notifyFirstNonMeAptorToUpdateStatus()
        {
            Debug.Log("AD in notifyFirstNonMeAptorToUpdateStatus");
            Part ap = getFirstAptorPartThatIsNotMe();
            Debug.Log("AD in notifyFirstNonMeAptorToUpdateStatus post gfaptinm");
            if (ap != null)
            {

                AptorDevice ad = ap.Modules.OfType<AptorDevice>().FirstOrDefault();
                if (ad != null)
                {
                    if (ad.isMaster() == isPrimaryAptorDevice)
                    {
                        Debug.Log("AD update other at next frame");
                        ad.updatePrimaryStatusNow();

                    }
                }
            }
            else
            {
                Debug.Log("AD no other part found");
            }
        }
        private void OnAptorActivation()
        {
            Debug.Log("AD gained primary status");
            isPrimaryAptorDevice = true;
            notifyFirstNonMeAptorToUpdateStatus();
            ActivateWindow();
        }
        private void OnAptorDeactivation()
        {
            Debug.Log("AD lost primary status");
            DeactivateWindow();
            isPrimaryAptorDevice = false;
            isInShutdown = true;
            notifyFirstNonMeAptorToUpdateStatus();
            isInShutdown = false;
        }

        public void ActivateWindow()
        {
            if (windowViewable)
            {
                Debug.LogError("AD Activate window while active");
                return;
            }
            windowViewable = true;
            RenderingManager.AddToPostDrawQueue(drawQueueNumber, new Callback(showWindow));
        }
        public void DeactivateWindow()
        {
            if (!windowViewable)
            {
                Debug.LogError("AD Deactivate window while inactive");
                return;
            }
            windowViewable = false;
            RenderingManager.RemoveFromPostDrawQueue(drawQueueNumber, new Callback(showWindow));
        }

        private void showWindow()
        {
            //Debug.Log("AD in show window");
            if (doUpdatePrimaryStatusAtNextFrame)
            {
                doUpdatePrimaryStatusAtNextFrame = false;
                updatePrimaryAptorDeviceStatus();
            }

            GUI.skin = HighLogic.Skin;
            windowPos = GUILayout.Window(windowId, windowPos, CreateWindowContents, "Stage Infos");
        }
        private void CreateWindowContents(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("I am " + idd);
            if (GUILayout.Button("Send Socket Message",GUILayout.ExpandWidth(false)))
            {
                print("Send Socket");
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

    }
}
