/**
 * EditorAptorDevice.cs - Behaviour of the Aptor Device in the Editor
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
    public partial class AptorDevice : PartModule
    {
        private void OnEditorStart()
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
        private void showEditorWindow()
        {
            onTick();
            //Debug.Log("AD in show window");
            GUI.skin = HighLogic.Skin;
            windowPos = GUILayout.Window(windowId, windowPos, CreateEditorWindowContents, "Stage Infos");
        }
        private void CreateEditorWindowContents(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("I am " + idd + " in " + computationStatus.ToString());

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Port", GUILayout.ExpandWidth(false));
                targetPortString = GUILayout.TextField(targetPortString, 5, GUILayout.ExpandWidth(false));
                GUILayout.Label("Iterations", GUILayout.ExpandWidth(false));
                iterations = GUILayout.TextField(iterations, 5, GUILayout.ExpandWidth(false));
                GUILayout.Label("Nodes", GUILayout.ExpandWidth(false));
                nodes = GUILayout.TextField(nodes, 10, GUILayout.ExpandWidth(false));

            }
            GUILayout.EndHorizontal();
            AscentCalculationButton();

            if (computationStatus == ComputationStatus.ComputationFinished)
            {
                if (GUILayout.Button("Log Pitch Thrust", GUILayout.ExpandWidth(false)))
                {
                    string lo = "";
                    foreach (ascentpoint ap in ascent)
                    {
                        lo += ap.time + ": " + Math.Round(100 * ap.thrust, 1) + "% / " + Math.Round(ap.pitch, 1) + "°\n";
                    }
                    log(lo);
                }
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void AscentCalculationButton()
        {
            if (canDoAscentCalculation())
            {
                if (GUILayout.Button("Perform Ascent Calculation", GUILayout.ExpandWidth(false)))
                {
                    RocketSpec rs = new RocketSpec(getRoot());
                    log("Pres Send Socket");
                    PerformAscentCalculation(rs);
                    log("Send Socket");
                }
            }
            else if (duringAscentCalculation())
            {
                GUILayout.Label("During Ascent Calculation ... please wait");
            }
        }

    }
}
