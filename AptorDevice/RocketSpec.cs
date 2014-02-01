/**
 * RocketSpecs.cs - Rocket Specification
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
    class RocketSpec
    {
        static private void log(string res)
        {
            Debug.Log("ADRS: " + res);
        }
        System.Text.RegularExpressions.Regex zeroMassPartRegex = new System.Text.RegularExpressions.Regex
    ("^(?:FTX-2 External Fuel Duct|EAS-4 Strut Connector|Octagonal Strut|Cubic Octagonal Strut|TT18-A Launch Stability Enhancer)$");

        public struct Engine
        {
            public double thrust;
            public double isp0;
            public double ispV;
            public int ignitionStage; // stage this engine is ignited at the beginning of
            public int burnoutStage; // stage this engine burns out at the end of
            public Engine(double _thrust, double _isp0, double _ispV, int _ignitionStage, int _burnoutStage)
            {
                thrust = _thrust;
                isp0 = _isp0;
                ispV = _ispV;
                ignitionStage = _ignitionStage;
                burnoutStage = _burnoutStage;
            }

        }
        public struct Stage
        {
            public double stageMass;
            public double initialMass;
            public double fuelMass;
            public double drag;
            public double oxidizer;
            public double fuel;
            public void IncStageMass(double mass)
            {
                log("mass " + stageMass + " " + mass);
                stageMass += mass;
                log("mass+ " + stageMass + " " + mass);
            }
            public void IncOxidizer(double oxi) { oxidizer += oxi; }
            public void IncFuel(double fue) { fuel+= fue; }
        }

        public struct Rocket {
            public List<Stage> stages;
            public List<Engine> engines;
            };
        private Rocket rocket = new Rocket();

        public Rocket getRocketSpec()
        {
            updateRocketSpec();

            return rocket;
        }

        private void updateRocketSpec()
        {
    
            if (rocket.stages == null) {
                //log("stages null");
                rocket.stages = new List<Stage>();
            }
            if (rocket.engines == null) {
                //log("engines null");
                rocket.engines = new List<Engine>();
            }

            rocket.stages.Clear();
            rocket.engines.Clear();
            Part root = getRoot();
            if (root == null)
            {
                log("root is null!!! should not be");
            }
            else
            {
                updateRocketPart(root, 0);
            }
            for ( int i = 0; i < rocket.stages.Count; i++)
            {
                Stage s = rocket.stages[i]; // is there a way to edit this without copying?
                s.drag = 0.2;
                log("drag of stage " + (i + 1) + " is " + s.drag);
                s.initialMass = s.stageMass;
                if (i > 0)
                {
                    s.initialMass += rocket.stages[i - 1].initialMass;
                }
                log("initial mass of stage " + (i + 1) + " is " + s.initialMass);
                s.fuelMass = Math.Min(s.fuel / 90, s.oxidizer / 110);
                log("fuel mass of stage " + (i + 1) + " is " + s.fuelMass);
                rocket.stages[i] = s;
            }
        }

        private void updateRocketPart(Part p, int stage)
        {
            //log("in updateRocketPart " + p.name);
            ModuleEngines engine = p.Modules.OfType<ModuleEngines>().FirstOrDefault();
            ModuleDecouple decoupler = p.Modules.OfType<ModuleDecouple>().FirstOrDefault();
            ModuleAnchoredDecoupler adecoupler = p.Modules.OfType<ModuleAnchoredDecoupler>().FirstOrDefault();

           // log("pre engine");
            int stageForMass; // stage that this part's mass should count
            if (engine != null)
            {
                float thrust = engine.maxThrust;
                float isp_vac = engine.atmosphereCurve.Evaluate(0);
                float isp_1atm = engine.atmosphereCurve.Evaluate(1);

                stageForMass = stage;
                Engine x = new Engine(engine.maxThrust, engine.atmosphereCurve.Evaluate(1), engine.atmosphereCurve.Evaluate(0), p.inverseStage, stage);
                //output.Add("Engine " + p.name + ": Stage = [" + stage + ", " + p.inverseStage + "] mass = " + p.mass + " thrust = " + thrust + " a0 " + engine.atmosphereCurve.Evaluate(0) + " a1 " + engine.atmosphereCurve.Evaluate(1));
                rocket.engines.Add(x);
            }
            else if (isDecoupler(p))
            {
                stageForMass = p.inverseStage + 1;
            }
            else
            {
                stageForMass = stage;
            }
            float dryMassOfPart = p.mass;
            if (zeroMassPartRegex.IsMatch(p.partInfo.title))
            {
                log("zero mass: " + p.partInfo.title);
                dryMassOfPart = 0;
            }
            else
            {
                log("AAAA " + p.partInfo.title);
            }

            //log("post engine");

            addMassToStage(stageForMass, dryMassOfPart + p.GetResourceMass());
            //log("post stage mass");
            addFuelToStage(stageForMass, p);
            //log("out updateRocketPart " + p.name);

            foreach (Part child in p.children)
            {
                updateRocketPart(child, stageForMass);
            }

        }

        private bool isDecoupler(Part p)
        {
            return p.Modules.OfType<ModuleDecouple>().FirstOrDefault() != null ||
                   p.Modules.OfType<ModuleAnchoredDecoupler>().FirstOrDefault() != null;

        }

        private void addMassToStage(int stageForMass, float mass)
        {
            extendStages(stageForMass);
            Stage s = rocket.stages[stageForMass]; // is there a way to edit this without copying?
            s.stageMass += mass;
            rocket.stages[stageForMass] = s;
            log("new temp stage mass of stage " + stageForMass + " is " + rocket.stages[stageForMass].stageMass);
        }

        private void extendStages(int stageForMass)
        {
            for (int i = rocket.stages.Count; i <= stageForMass; i++)
            {
                rocket.stages.Add(new Stage());
            }
        }

        private void addFuelToStage(int stageForMass, Part p)
        {
            extendStages(stageForMass);
            Stage s = rocket.stages[stageForMass]; // is there a way to edit this without copying?
            foreach (PartResource r in p.Resources)
            {
                if (r.resourceName == "LiquidFuel")
                {
                    s.IncFuel(r.amount);
                }
                else if (r.resourceName == "Oxidizer")
                {
                    s.IncOxidizer(r.amount);
                }
                log("adding fuel to stage " + stageForMass + ": " + r.info.name + " " + r.resourceName + " " + r.name + " " + r.amount);
            }
            rocket.stages[stageForMass] = s;
        }

        public Part getRoot()
        {
            List<Part> pl = Utility.getPartList();
            if (pl != null) {
                return pl.First();
            } else {
                return null;
            }
        }
        public void Print()
        {
            string res = "";
            foreach (Stage s in rocket.stages)
            {
                res += "ADD_STAGE " + s.initialMass + " " + s.fuelMass + " " + s.drag + "\n";
            }
            foreach (Engine e in rocket.engines)
            {
                res += "ENGINE " + e.thrust + " " + e.isp0 + " " + e.ispV + " " + e.ignitionStage + " " + e.burnoutStage + "\n";
            }
            Debug.Log(res);
        }
    }
}
