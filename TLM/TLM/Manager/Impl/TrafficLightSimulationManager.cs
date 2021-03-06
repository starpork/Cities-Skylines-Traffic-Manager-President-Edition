using System;
using ColossalFramework;
using TrafficManager.Geometry;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using TrafficManager.TrafficLight;
using TrafficManager.Traffic;
using System.Linq;
using CSUtil.Commons;
using TrafficManager.TrafficLight.Impl;
using TrafficManager.Geometry.Impl;

namespace TrafficManager.Manager.Impl {
	public class TrafficLightSimulationManager : AbstractNodeGeometryObservingManager, ICustomDataManager<List<Configuration.TimedTrafficLights>>, ITrafficLightSimulationManager {
		public static readonly TrafficLightSimulationManager Instance = new TrafficLightSimulationManager();
		public const int SIM_MOD = 64;
	
		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		internal TrafficLightSimulation[] TrafficLightSimulations = new TrafficLightSimulation[NetManager.MAX_NODE_COUNT];
		//public Dictionary<ushort, TrafficLightSimulation> TrafficLightSimulations = new Dictionary<ushort, TrafficLightSimulation>();

		private TrafficLightSimulationManager() {
			
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Traffic light simulations:");
			for (int i = 0; i < TrafficLightSimulations.Length; ++i) {
				if (TrafficLightSimulations[i] == null) {
					continue;
				}
				Log._Debug($"Simulation {i}: {TrafficLightSimulations[i]}");
			}
		}

		public void SimulationStep() {
			int frame = (int)(Singleton<SimulationManager>.instance.m_currentFrameIndex & (SIM_MOD - 1));
			int minIndex = frame * (NetManager.MAX_NODE_COUNT / SIM_MOD);
			int maxIndex = (frame + 1) * (NetManager.MAX_NODE_COUNT / SIM_MOD) - 1;

			for (int nodeId = minIndex; nodeId <= maxIndex; ++nodeId) {
				try {
					TrafficLightSimulation nodeSim = TrafficLightSimulations[nodeId];

					if (nodeSim != null && nodeSim.IsTimedLightActive()) {
						//Flags.applyNodeTrafficLightFlag((ushort)nodeId);
						nodeSim.TimedLight.SimulationStep();
					}
				} catch (Exception ex) {
					Log.Warning($"Error occured while simulating traffic light @ node {nodeId}: {ex.ToString()}");
				}
			}
		}

		/// <summary>
		/// Adds a traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public ITrafficLightSimulation AddNodeToSimulation(ushort nodeId) {
			TrafficLightSimulation sim = TrafficLightSimulations[nodeId];
			if (sim != null) {
				return sim;
			}
			TrafficLightSimulations[nodeId] = sim = new TrafficLightSimulation(nodeId);
			SubscribeToNodeGeometry(nodeId);
			return sim;
		}

		/// <summary>
		/// Destroys the traffic light and removes it
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="destroyGroup"></param>
		public void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup, bool removeTrafficLight) {
#if DEBUG
			Log.Warning($"TrafficLightSimulationManager.RemoveNodeFromSimulation({nodeId}, {destroyGroup}, {removeTrafficLight}) called.");
#endif

			TrafficLightSimulation sim = TrafficLightSimulations[nodeId];
			if (sim == null) {
				return;
			}
			TrafficLightManager tlm = TrafficLightManager.Instance;

			if (sim.TimedLight != null) {
				// remove/destroy all timed traffic lights in group
				List<ushort> oldNodeGroup = new List<ushort>(sim.TimedLight.NodeGroup);
				foreach (var timedNodeId in oldNodeGroup) {
					var otherNodeSim = GetNodeSimulation(timedNodeId);
					if (otherNodeSim == null) {
						continue;
					}

					if (destroyGroup || timedNodeId == nodeId) {
						//Log._Debug($"Slave: Removing simulation @ node {timedNodeId}");
						otherNodeSim.DestroyTimedTrafficLight();
						otherNodeSim.DestroyManualTrafficLight();
						((TrafficLightSimulation)otherNodeSim).NodeGeoUnsubscriber?.Dispose();
						RemoveNodeFromSimulation(timedNodeId);
						if (removeTrafficLight) {
							Constants.ServiceFactory.NetService.ProcessNode(timedNodeId, delegate (ushort nId, ref NetNode node) {
								tlm.RemoveTrafficLight(timedNodeId, ref node);
								return true;
							});
						}
					} else {
						otherNodeSim.TimedLight.RemoveNodeFromGroup(nodeId);
					}
				}
			}

			//Flags.setNodeTrafficLight(nodeId, false);
			//sim.DestroyTimedTrafficLight();
			sim.DestroyManualTrafficLight();
			sim.NodeGeoUnsubscriber?.Dispose();
			RemoveNodeFromSimulation(nodeId);
			if (removeTrafficLight) {
				Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
					tlm.RemoveTrafficLight(nodeId, ref node);
					return true;
				});
			}
		}

		public bool HasSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId] != null;
		}

		public bool HasTimedSimulation(ushort nodeId) {
			TrafficLightSimulation sim = TrafficLightSimulations[nodeId];
			if (sim == null) {
				return false;
			}
			return sim.IsTimedLight();
		}

		public bool HasActiveTimedSimulation(ushort nodeId) {
			TrafficLightSimulation sim = TrafficLightSimulations[nodeId];
			if (sim == null) {
				return false;
			}
			return sim.IsTimedLightActive();
		}

		public bool HasActiveSimulation(ushort nodeId) {
			TrafficLightSimulation sim = TrafficLightSimulations[nodeId];
			if (sim == null) {
				return false;
			}
			return sim.IsManualLight() || sim.IsTimedLightActive();
		}

		private void RemoveNodeFromSimulation(ushort nodeId) {
#if DEBUG
			Log.Warning($"TrafficLightSimulationManager.RemoveNodeFromSimulation({nodeId}) called.");
#endif

			TrafficLightSimulations[nodeId]?.Destroy();
			TrafficLightSimulations[nodeId] = null;
			UnsubscribeFromNodeGeometry(nodeId);
		}

		public ITrafficLightSimulation GetNodeSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId];
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				TrafficLightSimulations[nodeId] = null;
			}
		}

		protected override void HandleInvalidNode(NodeGeometry geometry) {
			RemoveNodeFromSimulation(geometry.NodeId, false, true);
		}

		protected override void HandleValidNode(NodeGeometry geometry) {
			
		}

		public bool LoadData(List<Configuration.TimedTrafficLights> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} timed traffic lights (new method)");

			TrafficLightManager tlm = TrafficLightManager.Instance;

			HashSet<ushort> nodesWithSimulation = new HashSet<ushort>();
			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				nodesWithSimulation.Add(cnfTimedLights.nodeId);
			}

			Dictionary<ushort, ushort> masterNodeIdBySlaveNodeId = new Dictionary<ushort, ushort>();
			Dictionary<ushort, List<ushort>> nodeGroupByMasterNodeId = new Dictionary<ushort, List<ushort>>();
			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				try {
					// TODO most of this should not be necessary at all if the classes around TimedTrafficLights class were properly designed
					List<ushort> currentNodeGroup = cnfTimedLights.nodeGroup.Distinct().ToList(); // enforce uniqueness of node ids
					if (!currentNodeGroup.Contains(cnfTimedLights.nodeId))
						currentNodeGroup.Add(cnfTimedLights.nodeId);
					// remove any nodes that are not configured to have a simulation
					currentNodeGroup = new List<ushort>(currentNodeGroup.Intersect(nodesWithSimulation));

					// remove invalid nodes from the group; find if any of the nodes in the group is already a master node
					ushort masterNodeId = 0;
					int foundMasterNodes = 0;
					for (int i = 0; i < currentNodeGroup.Count;) {
						ushort nodeId = currentNodeGroup[i];
						if (!Services.NetService.IsNodeValid(currentNodeGroup[i])) {
							currentNodeGroup.RemoveAt(i);
							continue;
						} else if (nodeGroupByMasterNodeId.ContainsKey(nodeId)) {
							// this is a known master node
							if (foundMasterNodes > 0) {
								// we already found another master node. ignore this node.
								currentNodeGroup.RemoveAt(i);
								continue;
							}
							// we found the first master node
							masterNodeId = nodeId;
							++foundMasterNodes;
						}
						++i;
					}

					if (masterNodeId == 0) {
						// no master node defined yet, set the first node as a master node
						masterNodeId = currentNodeGroup[0];
					}

					// ensure the master node is the first node in the list (TimedTrafficLights depends on this at the moment...)
					currentNodeGroup.Remove(masterNodeId);
					currentNodeGroup.Insert(0, masterNodeId);

					// update the saved node group and master-slave info
					nodeGroupByMasterNodeId[masterNodeId] = currentNodeGroup;
					foreach (ushort nodeId in currentNodeGroup) {
						masterNodeIdBySlaveNodeId[nodeId] = masterNodeId;
					}
				} catch (Exception e) {
					Log.Warning($"Error building timed traffic light group for TimedNode {cnfTimedLights.nodeId} (NodeGroup: {string.Join(", ", cnfTimedLights.nodeGroup.Select(x => x.ToString()).ToArray())}): " + e.ToString());
					success = false;
				}
			}

			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				try {
					if (!masterNodeIdBySlaveNodeId.ContainsKey(cnfTimedLights.nodeId))
						continue;
					ushort masterNodeId = masterNodeIdBySlaveNodeId[cnfTimedLights.nodeId];
					List<ushort> nodeGroup = nodeGroupByMasterNodeId[masterNodeId];

					Log._Debug($"Adding timed light at node {cnfTimedLights.nodeId}. NodeGroup: {string.Join(", ", nodeGroup.Select(x => x.ToString()).ToArray())}");

					ITrafficLightSimulation sim = AddNodeToSimulation(cnfTimedLights.nodeId);
					sim.SetupTimedTrafficLight(nodeGroup);
					var timedNode = sim.TimedLight;

					int j = 0;
					foreach (Configuration.TimedTrafficLightsStep cnfTimedStep in cnfTimedLights.timedSteps) {
						Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
						ITimedTrafficLightsStep step = timedNode.AddStep(cnfTimedStep.minTime, cnfTimedStep.maxTime, (TrafficLight.StepChangeMetric)cnfTimedStep.changeMetric, cnfTimedStep.waitFlowBalance);

						foreach (KeyValuePair<ushort, Configuration.CustomSegmentLights> e in cnfTimedStep.segmentLights) {
							if (!Services.NetService.IsSegmentValid(e.Key))
								continue;
							e.Value.nodeId = cnfTimedLights.nodeId;

							Log._Debug($"Loading timed step {j}, segment {e.Key} at node {cnfTimedLights.nodeId}");
							ICustomSegmentLights lights = null;
							if (!step.CustomSegmentLights.TryGetValue(e.Key, out lights)) {
								Log._Debug($"No segment lights found at timed step {j} for segment {e.Key}, node {cnfTimedLights.nodeId}");
								continue;
							}
							Configuration.CustomSegmentLights cnfLights = e.Value;

							Log._Debug($"Loading pedestrian light @ seg. {e.Key}, step {j}: {cnfLights.pedestrianLightState} {cnfLights.manualPedestrianMode}");

							lights.ManualPedestrianMode = cnfLights.manualPedestrianMode;
							lights.PedestrianLightState = cnfLights.pedestrianLightState;

							foreach (KeyValuePair<ExtVehicleType, Configuration.CustomSegmentLight> e2 in cnfLights.customLights) {
								Log._Debug($"Loading timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
								ICustomSegmentLight light = null;
								if (!lights.CustomLights.TryGetValue(e2.Key, out light)) {
									Log._Debug($"No segment light found for timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
									continue;
								}
								Configuration.CustomSegmentLight cnfLight = e2.Value;

								light.InternalCurrentMode = (TrafficLight.LightMode)cnfLight.currentMode; // TODO improve & remove
								light.SetStates(cnfLight.mainLight, cnfLight.leftLight, cnfLight.rightLight, false);
							}
						}
						++j;
					}
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from TimedNode (new method): " + e.ToString());
					success = false;
				}
			}

			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				try {
					ITrafficLightSimulation sim = GetNodeSimulation(cnfTimedLights.nodeId);
					if (sim == null || sim.TimedLight == null)
						continue;

					var timedNode = sim.TimedLight;

					timedNode.Housekeeping();
					if (cnfTimedLights.started)
						timedNode.Start(cnfTimedLights.currentStep);
				} catch (Exception e) {
					Log.Warning($"Error starting timed light @ {cnfTimedLights.nodeId}: " + e.ToString());
					success = false;
				}
			}

			return success;
		}

		public List<Configuration.TimedTrafficLights> SaveData(ref bool success) {
			List<Configuration.TimedTrafficLights> ret = new List<Configuration.TimedTrafficLights>();
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				try {
					ITrafficLightSimulation sim = GetNodeSimulation((ushort)nodeId);
					if (sim == null || !sim.IsTimedLight()) {
						continue;
					}

					Log._Debug($"Going to save timed light at node {nodeId}.");

					var timedNode = sim.TimedLight;
					timedNode.OnGeometryUpdate();

					Configuration.TimedTrafficLights cnfTimedLights = new Configuration.TimedTrafficLights();
					ret.Add(cnfTimedLights);

					cnfTimedLights.nodeId = timedNode.NodeId;
					cnfTimedLights.nodeGroup = new List<ushort>(timedNode.NodeGroup);
					cnfTimedLights.started = timedNode.IsStarted();
					int stepIndex = timedNode.CurrentStep;
					if (timedNode.IsStarted() && timedNode.GetStep(timedNode.CurrentStep).IsInEndTransition()) {
						// if in end transition save the next step
						stepIndex = (stepIndex + 1) % timedNode.NumSteps();
					}
					cnfTimedLights.currentStep = stepIndex;
					cnfTimedLights.timedSteps = new List<Configuration.TimedTrafficLightsStep>();

					for (var j = 0; j < timedNode.NumSteps(); j++) {
						Log._Debug($"Saving timed light step {j} at node {nodeId}.");
						ITimedTrafficLightsStep timedStep = timedNode.GetStep(j);
						Configuration.TimedTrafficLightsStep cnfTimedStep = new Configuration.TimedTrafficLightsStep();
						cnfTimedLights.timedSteps.Add(cnfTimedStep);

						cnfTimedStep.minTime = timedStep.MinTime;
						cnfTimedStep.maxTime = timedStep.MaxTime;
						cnfTimedStep.changeMetric = (int)timedStep.ChangeMetric;
						cnfTimedStep.waitFlowBalance = timedStep.WaitFlowBalance;
						cnfTimedStep.segmentLights = new Dictionary<ushort, Configuration.CustomSegmentLights>();
						foreach (KeyValuePair<ushort, ICustomSegmentLights> e in timedStep.CustomSegmentLights) {
							Log._Debug($"Saving timed light step {j}, segment {e.Key} at node {nodeId}.");

							ICustomSegmentLights segLights = e.Value;
							Configuration.CustomSegmentLights cnfSegLights = new Configuration.CustomSegmentLights();

							ushort lightsNodeId = segLights.NodeId;
							if (lightsNodeId == 0 || lightsNodeId != timedNode.NodeId) {
								Log.Warning($"Inconsistency detected: Timed traffic light @ node {timedNode.NodeId} contains custom traffic lights for the invalid segment ({segLights.SegmentId}) at step {j}: nId={lightsNodeId}");
								continue;
							}

							cnfSegLights.nodeId = lightsNodeId; // TODO not needed
							cnfSegLights.segmentId = segLights.SegmentId; // TODO not needed
							cnfSegLights.customLights = new Dictionary<ExtVehicleType, Configuration.CustomSegmentLight>();
							cnfSegLights.pedestrianLightState = segLights.PedestrianLightState;
							cnfSegLights.manualPedestrianMode = segLights.ManualPedestrianMode;

							cnfTimedStep.segmentLights.Add(e.Key, cnfSegLights);

							Log._Debug($"Saving pedestrian light @ seg. {e.Key}, step {j}: {cnfSegLights.pedestrianLightState} {cnfSegLights.manualPedestrianMode}");

							foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e2 in segLights.CustomLights) {
								Log._Debug($"Saving timed light step {j}, segment {e.Key}, vehicleType {e2.Key} at node {nodeId}.");

								ICustomSegmentLight segLight = e2.Value;
								Configuration.CustomSegmentLight cnfSegLight = new Configuration.CustomSegmentLight();
								cnfSegLights.customLights.Add(e2.Key, cnfSegLight);

								cnfSegLight.nodeId = lightsNodeId; // TODO not needed
								cnfSegLight.segmentId = segLights.SegmentId; // TODO not needed
								cnfSegLight.currentMode = (int)segLight.CurrentMode;
								cnfSegLight.leftLight = segLight.LightLeft;
								cnfSegLight.mainLight = segLight.LightMain;
								cnfSegLight.rightLight = segLight.LightRight;
							}
						}
					}
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving timed traffic light @ {nodeId}: {e.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}
