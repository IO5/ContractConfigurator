﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using KSP.Localization;

namespace ContractConfigurator.Parameters
{
    /// <summary>
    /// Parameter for checking for a rendezvous between two craft.
    /// </summary>
    public class Rendezvous : VesselParameter
    {
        protected List<string> vessels { get; set; }
        protected double distance { get; set; }
        protected float updateFrequency { get; set; }

        private Vessel[] dockedVessels = new Vessel[2];
        private float lastUpdate = 0.0f;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.50f;
        private List<VesselWaypoint> vesselWaypoints = new List<VesselWaypoint>();

        public Rendezvous()
            : base(null)
        {
        }

        public Rendezvous(IEnumerable<string> vessels, double distance, string title, float updateFrequency)
            : base(title)
        {
            this.vessels = vessels.ToList();
            this.distance = distance;
            this.title = title;
            this.updateFrequency = updateFrequency;
            disableOnStateChange = true;
        }

        protected override string GetParameterTitle()
        {
            string output = "";
            if (string.IsNullOrEmpty(title))
            {
                if (Parent is VesselParameterGroup)
                {
                    output = Localizer.Format("#cc.param.Rendezvous.1", vessels.Count > 0 ? ContractVesselTracker.GetDisplayName(vessels[0]) : Localizer.GetStringByTag("#cc.param.vessel.Any"));
                }
                else
                {
                    output = Localizer.Format("#cc.param.Rendezvous.2", ContractVesselTracker.GetDisplayName(vessels[0]), vessels.Count > 1 ? ContractVesselTracker.GetDisplayName(vessels[1]) : Localizer.GetStringByTag("#cc.param.vessel.any"));
                }
            }
            else
            {
                output = title;
            }
            return output;
        }

        protected override void OnRegister()
        {
            base.OnRegister();

            // Add a waypoint for each possible vessel in the list
            foreach (string vesselKey in vessels)
            {
                VesselWaypoint vesselWaypoint = new VesselWaypoint(Root, vesselKey);
                vesselWaypoints.Add(vesselWaypoint);
                vesselWaypoint.Register();
            }
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();

            foreach (VesselWaypoint vesselWaypoint in vesselWaypoints)
            {
                vesselWaypoint.Unregister();
            }
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("distance", distance);
            foreach (string vessel in vessels)
            {
                node.AddValue("vessel", vessel);
            }
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            vessels = ConfigNodeUtil.ParseValue<List<string>>(node, "vessel", new List<string>());
            distance = ConfigNodeUtil.ParseValue<double>(node, "distance");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (UnityEngine.Time.fixedTime - lastUpdate > updateFrequency)
            {
                lastUpdate = UnityEngine.Time.fixedTime;

                Vessel v1 = null;
                Vessel v2 = null;

                if (Parent is VesselParameterGroup)
                {
                    v1 = ((VesselParameterGroup)Parent).TrackedVessel ?? FlightGlobals.ActiveVessel;
                    v2 = vessels.Count > 0 ? ContractVesselTracker.Instance.GetAssociatedVessel(vessels[0]) : null;

                    // No vessel association
                    if (vessels.Count > 0 && v2 == null || v1 == v2)
                    {
                        return;
                    }
                }
                else
                {
                    v1 = ContractVesselTracker.Instance.GetAssociatedVessel(vessels[0]);
                    v2 = vessels.Count > 1 ? ContractVesselTracker.Instance.GetAssociatedVessel(vessels[1]) : null;

                    // No vessel association
                    if (v1 == null || vessels.Count > 1 && v2 == null)
                    {
                        return;
                    }
                }

                LoggingUtil.LogVerbose(this, "v1 = {0}", (v1 == null ? "null" : v1.id.ToString()));
                LoggingUtil.LogVerbose(this, "v2 = {0}", (v2 == null ? "null" : v2.id.ToString()));

                if (v1 == null)
                {
                    return;
                }

                bool forceStateChange = false;
                bool rendezvous = false;

                // Distance to a specific craft
                if (v2 != null)
                {
                    float distance = Vector3.Distance(v1.transform.position, v2.transform.position);
                    if (distance < this.distance)
                    {
                        rendezvous = true;
                        forceStateChange |= SetState(v1, ParameterState.Complete);
                        forceStateChange |= SetState(v2, ParameterState.Complete);
                    }
                }
                else
                {
                    foreach (Vessel v in FlightGlobals.Vessels)
                    {
                        if (v != v1 && v.vesselType != VesselType.EVA && v.vesselType != VesselType.Debris && v.vesselType != VesselType.Flag)
                        {
                            float distance = Vector3.Distance(v1.transform.position, v.transform.position);
                            if (distance < this.distance)
                            {
                                rendezvous = true;
                                forceStateChange |= SetState(v1, ParameterState.Complete);
                                forceStateChange |= SetState(v, ParameterState.Complete);
                            }
                        }
                    }
                }

                if (rendezvous)
                {
                    CheckVessel(FlightGlobals.ActiveVessel, forceStateChange);
                }
            }
        }
        
        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Whether the vessel meets the condition</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);
            return GetState(vessel) == ParameterState.Complete;
        }
    }
}
