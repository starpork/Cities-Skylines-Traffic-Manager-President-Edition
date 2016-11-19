using ColossalFramework;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Manager;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Custom.PathFinding;
using System;
using static TrafficManager.Traffic.ExtCitizenInstance;
using TrafficManager.Util;
using ColossalFramework.Math;
using TrafficManager.UI;

namespace TrafficManager.Custom.AI {
	class CustomHumanAI : CitizenAI {
		public void CustomSimulationStep(ushort instanceID, ref CitizenInstance instanceData, Vector3 physicsLodRefPos) {
			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
				extInstance.UpdateReturnPathState();
			}
			// NON-STOCK CODE END

			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
				PathManager pathManager = Singleton<PathManager>.instance;
				byte pathFindFlags = pathManager.m_pathUnits.m_buffer[instanceData.m_path].m_pathFindFlags;

				// NON-STOCK CODE START
				bool pathFindFailed = (pathFindFlags & PathUnit.FLAG_FAILED) != 0;
				bool pathFindSucceeded = (pathFindFlags & PathUnit.FLAG_READY) != 0;
				bool handleSuccess = true;

				if (Options.prohibitPocketCars) {
					ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

					if (extInstance.ReturnPathState == ExtPathState.Calculating) {
						// wait for the return path being calculated
						return;
					} else if (extInstance.ReturnPathState == ExtPathState.Failed) {
						if (Options.debugSwitches[1])
							Log._Debug($"CustomPassengerCarAI.CustomSimulationStep: Return path {extInstance.ReturnPathId} FAILED. Forcing path-finding to fail.");
						pathFindSucceeded = false;
						pathFindFailed = true;
					}

					if (extInstance.ReturnPathState == ExtPathState.Ready || extInstance.ReturnPathState == ExtPathState.Failed)
						extInstance.ReleaseReturnPath();

					if (Options.debugSwitches[2] && (pathFindFailed || pathFindSucceeded)) {
						Log._Debug($"CustomHumanAI::CustomSimulationStep Citizen instance {instanceID}, citizen {instanceData.m_citizen} is called {Singleton<CitizenManager>.instance.GetCitizenName(instanceData.m_citizen)} and is {Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].Age} years old. PathMode={extInstance.PathMode} ReturnPathState={extInstance.ReturnPathState}");
					}

					if (pathFindSucceeded) {
						bool handleSoftPathFindFailure;
						if (!CustomHumanAI.OnPathFindSuccess(instanceID, ref instanceData, out handleSoftPathFindFailure, out handleSuccess)) {
							if (Options.debugSwitches[1]) {
								ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
								Log._Debug($"CustomHumanAI.CustomSimulationStep: " + (handleSoftPathFindFailure ? "Soft" : "Hard") + $" path-find failure: Citizen instance {instanceID} needs a new path. CurrentPathMode={extInstance.PathMode} parkedVehicleId={parkedVehicleId}");
							}

							if (handleSoftPathFindFailure) {
								instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
								instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
								this.InvalidPath(instanceID, ref instanceData);
								return;
							} else {
								pathFindSucceeded = false;
								pathFindFailed = true;
							}
						} else if (! handleSuccess) {
							return;
						}
					} else if (pathFindFailed) {
						if (CustomHumanAI.OnPathFindFailure(instanceID, ref instanceData, extInstance)) {
							if (Options.debugSwitches[1]) {
								ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
								Log._Debug($"CustomHumanAI.CustomSimulationStep: Handled path-find failure: Citizen instance {instanceID} needs a new path. CurrentPathMode={extInstance.PathMode} parkedVehicleId={parkedVehicleId}");
							}

							//GetBuildingTargetPosition(instanceID, ref instanceData, 0f);
							instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
							instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
							this.InvalidPath(instanceID, ref instanceData);
							return;
						}
					}
				}
				// NON-STOCK CODE END

				if (pathFindSucceeded) { // NON-STOCK CODE
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: (Vanilla) Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path} -- calling HumanAI.PathfindSuccess");
					this.Spawn(instanceID, ref instanceData);
					instanceData.m_pathPositionIndex = 255;
					instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
					instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
					this.PathfindSuccess(instanceID, ref instanceData);
				} else if (pathFindFailed) { // NON-STOCK CODE
					if (Options.debugSwitches[1])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: (Vanilla) Path-finding failed for citizen instance {instanceID}. Path: {instanceData.m_path} -- calling HumanAI.PathfindFailure");
					instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
					instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
					Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
					instanceData.m_path = 0u;
					this.PathfindFailure(instanceID, ref instanceData);
					return;
				}
			}

			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				//ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

				/*if (extInstance.PathMode == ExtPathMode.ReachingParkedCar) {
					ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;

					CitizenInstance.Frame frameData = instanceData.GetLastFrameData();
					frameData.m_velocity = (Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position - frameData.m_position) * 0.5f;
					if (frameData.m_velocity.sqrMagnitude > 1f) {
						frameData.m_velocity.Normalize();
					}
					if (frameData.m_velocity.sqrMagnitude > 0.01f) {
						frameData.m_rotation = Quaternion.LookRotation(frameData.m_velocity);
					}
				}*/

				if (CustomHumanAI.HasReachedParkedCar(instanceID, ref instanceData)) {
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Citizen instance {instanceID} arrives at parked car.");

					this.InvalidPath(instanceID, ref instanceData);
					return;
				}
			}
			// NON-STOCK CODE END

			base.SimulationStep(instanceID, ref instanceData, physicsLodRefPos);

			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			ushort vehicleId = 0;
			if (instanceData.m_citizen != 0u) {
				vehicleId = citizenManager.m_citizens.m_buffer[(int)((UIntPtr)instanceData.m_citizen)].m_vehicle;
			}
			if (vehicleId != 0) {
				VehicleInfo vehicleInfo = vehicleManager.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
					vehicleInfo.m_vehicleAI.SimulationStep(vehicleId, ref vehicleManager.m_vehicles.m_buffer[(int)vehicleId], vehicleId, ref vehicleManager.m_vehicles.m_buffer[(int)vehicleId], 0);
					vehicleId = 0;
				}
			}
			if (vehicleId == 0 && (instanceData.m_flags & (CitizenInstance.Flags.Character | CitizenInstance.Flags.WaitingPath)) == CitizenInstance.Flags.None) {
				instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
				this.ArriveAtDestination(instanceID, ref instanceData, false);
				citizenManager.ReleaseCitizenInstance(instanceID);
			}
		}

		public bool CustomArriveAtTarget(ushort instanceID, ref CitizenInstance citizenData) { // TODO stock code
			if ((citizenData.m_flags & CitizenInstance.Flags.HangAround) != CitizenInstance.Flags.None) {
				uint citizenId = citizenData.m_citizen;
				if (citizenId != 0u) {
					CitizenManager instance = Singleton<CitizenManager>.instance;
					if (instance.m_citizens.m_buffer[(int)((UIntPtr)citizenId)].CurrentLocation == Citizen.Location.Moving) {
						this.ArriveAtDestination(instanceID, ref citizenData, true);
					}
					if (instance.m_citizens.m_buffer[(int)((UIntPtr)citizenId)].GetBuildingByLocation() == citizenData.m_targetBuilding) {
						return false;
					}
				}
				citizenData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
				citizenData.Unspawn(instanceID);
			} else {

				this.ArriveAtDestination(instanceID, ref citizenData, true);
			}
			return true;
		}

		internal static bool OnPathFindFailure(ushort instanceID, ref CitizenInstance instanceData, ExtCitizenInstance extInstance) {
			if (Options.debugSwitches[1])
				Log._Debug($"CustomHumanAI.OnPathFindFailure: Path-finding failed for citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.PathMode}");

			if (instanceData.m_targetBuilding != 0) {
				switch (extInstance.PathMode) {
					case ExtPathMode.CalculatingCarPathToTarget:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
						//ExtBuildingManager.Instance().GetExtBuilding(instanceData.m_targetBuilding).AddParkingSpaceDemand((uint)Options.debugValues[27]);
						break;
					case ExtPathMode.None:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToTarget:
						ExtBuildingManager.Instance().GetExtBuilding(instanceData.m_targetBuilding).AddPublicTransportDemand((uint)Options.debugValues[28], false);
                        if (instanceData.m_sourceBuilding != 0) {
                            ExtBuildingManager.Instance().GetExtBuilding(instanceData.m_sourceBuilding).AddPublicTransportDemand((uint)Options.debugValues[28], true);
                        }
						break;
				}
			}

			if (extInstance.PathMode == ExtPathMode.CalculatingWalkingPathToParkedCar) {
				// cannot reach parked car
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
					if (Options.debugSwitches[1])
						Log._Debug($"CustomHumanAI.OnPathFindFailure: Releasing parked vehicle {parkedVehicleId} for citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.PathMode}");
					Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedVehicleId);
				}
			}

			bool ret = false;

			switch (extInstance.PathMode) {
				case ExtPathMode.CalculatingCarPathToTarget:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
				case ExtPathMode.CalculatingWalkingPathToParkedCar:
					extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
					ret = true;
					break;
				default:
					extInstance.Reset();
					break;
			}

			if (Options.debugSwitches[1])
				Log._Debug($"CustomHumanAI.OnPathFindFailure: Setting CurrentPathMode for citizen instance {extInstance.InstanceId} to {extInstance.PathMode}, ret={ret}");

			return ret;
		}

		internal static bool OnPathFindSuccess(ushort instanceID, ref CitizenInstance instanceData, out bool handleSoftPathFindFailure, out bool handleSuccess) {
			handleSoftPathFindFailure = false;
			handleSuccess = true;
			if (Options.debugSwitches[2])
				Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path} vehicle={Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_vehicle}");

			if (Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_vehicle == 0) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
				float sqrDistToParkedVehicle = 0f;
				if (parkedVehicleId != 0) {
					sqrDistToParkedVehicle = (instanceData.GetLastFramePosition() - Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position).sqrMagnitude;
				}

				if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.None || (
					(extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget || extInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos) &&
					(parkedVehicleId == 0 || sqrDistToParkedVehicle >= Options.debugValues[30] + 1f)
					)) {

					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: PathMode={extInstance.PathMode}, parkedVehicleId={parkedVehicleId}, sqrDistToParkedVehicle={sqrDistToParkedVehicle} for citizen instance {instanceID}.");
					if ((CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes & (byte)(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} contains passenger car section and citizen is walking at the moment. Ensuring that citizen is allowed to use their car.");

						// check if citizen is at an outside connection
						bool isAtOutsideConnection = false;
						ushort sourceBuildingId = instanceData.m_sourceBuilding;
						if (sourceBuildingId != 0) {
							isAtOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;// Info.m_buildingAI is OutsideConnectionAI;
							//isAtOutsideConnection = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].Info.m_buildingAI is OutsideConnectionAI;
						}

						if (Options.debugSwitches[2] && isAtOutsideConnection) {
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is located at an incoming outside connection.");
						}

						/*if (sourceBuildingId != 0) {
							// check if current building is an outside connection
							bool isAtOutsideConnection = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].Info.m_buildingAI is OutsideConnectionAI;
							if (isAtOutsideConnection) {
								if (Options.debugSwitches[2])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is currently located at outside connection {sourceBuildingId}. Allowing pocket cars.");
								extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.DrivingToTarget;xxx
								return true;
							}
						}*/

						if (!isAtOutsideConnection) {
							ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_homeBuilding;

							if (parkedVehicleId == 0) {
								if (Options.debugSwitches[1])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} does not have a parked vehicle! CurrentPathMode={extInstance.PathMode}");

								// try to spawn parked vehicle in the vicinity of the starting point.
								VehicleInfo vehicleInfo = null;
								PrefabAI ai = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].GetCitizenInfo(instanceData.m_citizen).GetAI();
								if (ai is ResidentAI) {
									vehicleInfo = CustomResidentAI.GetVehicleInfo(instanceID, ref instanceData, false);
								} else if (ai is TouristAI) {
									vehicleInfo = CustomTouristAI.GetVehicleInfo(instanceID, ref instanceData, false);
								}

								if (vehicleInfo != null) {
									switch (vehicleInfo.GetService()) {
										case ItemClass.Service.PublicTransport:
											if ((CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0) {
												extInstance.PathMode = ExtCitizenInstance.ExtPathMode.PublicTransportToTarget;
											} else {
												extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToTarget;
											}
											if (Options.debugSwitches[2])
												Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} is using public transport/is walking. CurrentPathMode={extInstance.PathMode}");
											return true;
										case ItemClass.Service.Residential:
											if (Options.debugSwitches[2])
												Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} is using their own passenger car. CurrentPathMode={extInstance.PathMode}");

											Vector3 currentPos;
											if (sourceBuildingId != 0) {
												currentPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_position;
												if (Options.debugSwitches[2])
													Log._Debug($"CustomHumanAI.OnPathFindSuccess: Taking current position from source building {sourceBuildingId} for citizen {instanceData.m_citizen} (citizen instance {instanceID}): {currentPos} CurrentPathMode={extInstance.PathMode}");
											} else {
												ushort currentBuildingId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].GetBuildingByLocation();
												if (currentBuildingId != 0) {
													currentPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[currentBuildingId].m_position;
													if (Options.debugSwitches[2])
														Log._Debug($"CustomHumanAI.OnPathFindSuccess: Taking current position from current building {currentBuildingId} for citizen {instanceData.m_citizen} (citizen instance {instanceID}): {currentPos}. CurrentPathMode={extInstance.PathMode}");
												} else {
													currentPos = instanceData.GetLastFramePosition();
													if (Options.debugSwitches[2])
														Log._Debug($"CustomHumanAI.OnPathFindSuccess: Taking current position from last frame position for citizen {instanceData.m_citizen} (citizen instance {instanceID}): {currentPos}. Home {homeId} pos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position} CurrentPathMode={extInstance.PathMode}");
												}
											}

											Vector3 parkPos;
											if (CustomCitizenAI.TrySpawnParkedPassengerCar(instanceData.m_citizen, homeId, currentPos, vehicleInfo, out parkPos)) {
												parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
												if (Options.debugSwitches[1] && sourceBuildingId != 0)
													Log._Debug($"Parked vehicle for citizen {instanceData.m_citizen} (instance {instanceID}) is {parkedVehicleId} now.");

												if (sourceBuildingId != 0) {
													ExtBuildingManager.Instance().GetExtBuilding(sourceBuildingId).ModifyParkingSpaceDemand(parkPos);
												}
											} else {
												if (Options.debugSwitches[1]) {
													Log._Debug($">> Failed to spawn parked vehicle for citizen {instanceData.m_citizen} (citizen instance {instanceID}). homePos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position}");
												}

												if (sourceBuildingId != 0) {
													ExtBuildingManager.Instance().GetExtBuilding(sourceBuildingId).AddParkingSpaceDemand(10u);
												}
											}
											break;
										default:
											extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToTarget;
											if (Options.debugSwitches[1])
												Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} is using an UNHANDLED {vehicleInfo.GetService()} vehicle. CurrentPathMode={extInstance.PathMode}");
											return true;
									}
								} else {
									if (Options.debugSwitches[1]) {
										Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId}, home {homeId} does not own a vehicle. vehicleInfo={vehicleInfo}, vehicleInfo.GetService()={vehicleInfo?.GetService()}");
									}
								}
							}

							if (parkedVehicleId != 0) {
								if (instanceData.m_targetBuilding != 0) {
									// check distance between parked vehicle and target building. If it is too small then the cim is walking/using transport to get to their target
									float parkedDistToTarget = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_position - Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position).magnitude;
									if (parkedDistToTarget < Options.debugValues[29]) {
										extInstance.PathMode = ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToTarget;
										handleSoftPathFindFailure = true;

										if (Options.debugSwitches[1])
											Log._Debug($"CustomHumanAI.OnPathFindSuccess: Parked vehicle {parkedVehicleId} of citizen instance {instanceID} is {parkedDistToTarget} units away from target building {instanceData.m_targetBuilding}. Forcing citizen to walk. PathMode={extInstance.PathMode}");

										return false;
									}
								}

								// citizen has to reach their parked vehicle first
								if (Options.debugSwitches[2])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Calculating path to reach parked vehicle {parkedVehicleId} for citizen instance {instanceID}. targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");

								extInstance.PathMode = ExtCitizenInstance.ExtPathMode.RequiresWalkingPathToParkedCar;
								//instanceData.m_targetPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
								//extInstance.ParkedVehiclePosition = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;

								handleSoftPathFindFailure = true;
								return false;
							} else {
								if (Options.debugSwitches[1])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} does not have a parked vehicle! Forcing path-finding to fail.");
									
								return false;
							}
						} else {
							extInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToTarget;

							if (Options.debugSwitches[1])
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}) is located at an outside connection: {sourceBuildingId} CurrentPathMode={extInstance.PathMode}");

							return true;
						}
					} else {
						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} does not contain passenger car section.");
						if ((CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0) {
							extInstance.PathMode = ExtCitizenInstance.ExtPathMode.PublicTransportToTarget;
						} else {
							extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToTarget;
						}
						return true;
					}
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar) {
					// path to parked vehicle has been calculated
					extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToParkedCar;
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now on their way to its parked vehicle. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
					return true;
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget) {
					// path using passenger car has been calculated
					ushort vehicleId;
					if (CustomHumanAI.EnterParkedCar(instanceID, ref instanceData, parkedVehicleId, out vehicleId)) {
						extInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToTarget;
						handleSuccess = false;

						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by car (vehicleId={vehicleId}). CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
						return true;
					} else {
						if (Options.debugSwitches[1])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Entering parked vehicle {parkedVehicleId} failed for citizen instance {instanceID}. GIVING UP. CurrentDepartureMode={extInstance.PathMode}");

						return false;
					}
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos) {
					// path using passenger car with known parking position has been calculated
					ushort vehicleId;
					if (CustomHumanAI.EnterParkedCar(instanceID, ref instanceData, parkedVehicleId, out vehicleId)) {
						extInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos;
						handleSuccess = false;

						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by car (vehicleId={vehicleId}) and knows where to park. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
						return true;
					} else {
						if (Options.debugSwitches[1])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Entering parked vehicle {parkedVehicleId} failed for citizen instance {instanceID}. GIVING UP. CurrentDepartureMode={extInstance.PathMode}");

						return false;
					}
					return true;
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToTarget) {
					// final walking path to target has been calculated
					extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToTarget;
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by foot to their final target. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
					return true;
				}
			}
			return true;
		}

		protected static bool EnterParkedCar(ushort instanceID, ref CitizenInstance instanceData, ushort parkedVehicleId, out ushort vehicleId) {
			VehicleManager vehManager = Singleton<VehicleManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;
			CitizenManager citManager = Singleton<CitizenManager>.instance;

			Vector3 parkedVehPos = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
			Quaternion parkedVehRot = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].m_rotation;
			VehicleInfo vehicleInfo = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].Info;

			PathUnit.Position vehLanePathPos;
			if (! CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(0, out vehLanePathPos)) {
				if (Options.debugSwitches[1])
					Log._Debug($"CustomHumanAI.EnterParkedCar: Could not get first car path position of citizen instance {instanceID}!");

				vehicleId = 0;
				return false;
			}
			uint vehLaneId = PathManager.GetLaneID(vehLanePathPos);

			Vector3 vehLanePos;
			float vehLaneOff;
			netManager.m_lanes.m_buffer[vehLaneId].GetClosestPosition(parkedVehPos, out vehLanePos, out vehLaneOff);
			byte vehLaneOffset = (byte)Mathf.Clamp(Mathf.RoundToInt(vehLaneOff * 255f), 0, 255);

			// movement vector from parked vehicle position to road position
			Vector3 forwardVector = parkedVehPos + Vector3.ClampMagnitude(vehLanePos - parkedVehPos, 5f);
			if (vehManager.CreateVehicle(out vehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkedVehPos, TransferManager.TransferReason.None, false, false)) {
				// update frame data
				Vehicle.Frame frame = vehManager.m_vehicles.m_buffer[(int)vehicleId].m_frame0;
				frame.m_rotation = parkedVehRot;

				vehManager.m_vehicles.m_buffer[vehicleId].m_frame0 = frame;
				vehManager.m_vehicles.m_buffer[vehicleId].m_frame1 = frame;
				vehManager.m_vehicles.m_buffer[vehicleId].m_frame2 = frame;
				vehManager.m_vehicles.m_buffer[vehicleId].m_frame3 = frame;
				vehicleInfo.m_vehicleAI.FrameDataUpdated(vehicleId, ref vehManager.m_vehicles.m_buffer[vehicleId], ref frame);

				// update vehicle target position
				vehManager.m_vehicles.m_buffer[vehicleId].m_targetPos0 = new Vector4(vehLanePos.x, vehLanePos.y, vehLanePos.z, 2f);

				// update other fields
				vehManager.m_vehicles.m_buffer[vehicleId].m_flags = (vehManager.m_vehicles.m_buffer[vehicleId].m_flags | Vehicle.Flags.Stopped);
				vehManager.m_vehicles.m_buffer[vehicleId].m_path = instanceData.m_path;
				vehManager.m_vehicles.m_buffer[vehicleId].m_pathPositionIndex = 0;
				vehManager.m_vehicles.m_buffer[vehicleId].m_lastPathOffset = vehLaneOffset;
				vehManager.m_vehicles.m_buffer[vehicleId].m_transferSize = (ushort)(instanceData.m_citizen & 65535u);

				if (! vehicleInfo.m_vehicleAI.TrySpawn(vehicleId, ref vehManager.m_vehicles.m_buffer[vehicleId])) {
					if (Options.debugSwitches[1])
						Log._Debug($"CustomHumanAI.EnterParkedCar: Could not spawn a {vehicleInfo.m_vehicleType} for citizen instance {instanceID}!");
					return false;
				}
				
				// change instances
				InstanceID parkedVehInstance = InstanceID.Empty;
				parkedVehInstance.ParkedVehicle = parkedVehicleId;
				InstanceID vehInstance = InstanceID.Empty;
				vehInstance.Vehicle = vehicleId;
				Singleton<InstanceManager>.instance.ChangeInstance(parkedVehInstance, vehInstance);

				// set vehicle id for citizen instance
				instanceData.m_path = 0u;
				citManager.m_citizens.m_buffer[instanceData.m_citizen].SetParkedVehicle(instanceData.m_citizen, 0);
				citManager.m_citizens.m_buffer[instanceData.m_citizen].SetVehicle(instanceData.m_citizen, vehicleId, 0u);

				// update citizen instance flags
				instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
				instanceData.m_flags &= ~CitizenInstance.Flags.EnteringVehicle;
				instanceData.m_flags &= ~CitizenInstance.Flags.TryingSpawnVehicle;
				instanceData.m_flags &= ~CitizenInstance.Flags.BoredOfWaiting;
				instanceData.m_waitCounter = 0;

				// unspawn citizen instance
				instanceData.Unspawn(instanceID);

				if (Options.debugSwitches[2])
					Log._Debug($"CustomHumanAI.EnterParkedCar: Citizen instance {instanceID} is now entering vehicle {vehicleId}. Set vehicle target position to {vehLanePos} (segment={vehLanePathPos.m_segment}, lane={vehLanePathPos.m_lane}, offset={vehLanePathPos.m_offset})");

				return true;
			} else {
				// failed to find a road position
				if (Options.debugSwitches[1])
					Log._Debug($"CustomHumanAI.EnterParkedCar: Could not find a road position for citizen instance {instanceID} near parked vehicle {parkedVehicleId}!");
				return false;
			}
		}

		internal static bool HasReachedParkedCar(ushort instanceID, ref CitizenInstance instanceData) {
			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

			if (instanceData.m_citizen == 0) {
				if (Options.debugSwitches[1])
					Log._Debug($"CustomHumanAI.NeedsCarPath: citizen instance {instanceID} is not assigned to a valid citizen!");
				extInstance.Reset();
				return false;
			}

			bool walkingToCar = extInstance.PathMode == ExtCitizenInstance.ExtPathMode.WalkingToParkedCar || extInstance.PathMode == ExtCitizenInstance.ExtPathMode.ReachingParkedCar;
			bool walkingToTarget = extInstance.PathMode == ExtCitizenInstance.ExtPathMode.WalkingToTarget || extInstance.PathMode == ExtCitizenInstance.ExtPathMode.PublicTransportToTarget;
			bool spawned = ((instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None);

			if (Options.debugSwitches[4] && (walkingToCar || walkingToTarget)) {
				bool? hasParkedVehicle = null;
				if (walkingToCar) {
					hasParkedVehicle = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle != 0;
				}
				
				Log._Debug($"CustomHumanAI.NeedsCarPath: called for citizen instance {instanceID}. walkingToCar={walkingToCar}, walkingToTarget={walkingToTarget}, spawned={spawned}, hasParkedVehicle={hasParkedVehicle}");
			}

			if (spawned && (walkingToCar || walkingToTarget)) {
				// check if path is complete
				PathUnit.Position pos;
				if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
					if (walkingToCar) {
						ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
						if (parkedVehicleId != 0) {
							instanceData.m_targetPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
							instanceData.m_targetPos.w = 1f;

							float dist = (instanceData.GetLastFramePosition() - (Vector3)instanceData.m_targetPos).sqrMagnitude;

							if (dist >= Options.debugValues[30]) {
								extInstance.PathMode = ExtPathMode.ReachingParkedCar;
								if (Options.debugSwitches[2])
									Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} is currently reaching their parked car {parkedVehicleId} (dist={dist}). CurrentDepartureMode={extInstance.PathMode}");
								return false;
							} else {
								extInstance.PathMode = ExtCitizenInstance.ExtPathMode.ParkedCarReached;
								if (Options.debugSwitches[2])
									Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached parking position (dist={dist}). Set targetPos to parked vehicle position. Calculating remaining path now. CurrentDepartureMode={extInstance.PathMode}");
								return true;
							}
						} else {
							extInstance.Reset();
							if (Options.debugSwitches[1])
								Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached parking position but does not own a parked car. Illegal state! Setting CurrentDepartureMode={extInstance.PathMode}");
							return false;
						}
					} else {
						extInstance.Reset();
						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached target. CurrentDepartureMode={extInstance.PathMode}");
						return false;
					}
				}
			}

			return false;
		}

		internal static string EnrichLocalizedStatus(string ret, ExtCitizenInstance extInstance) {
			if (extInstance != null) {
				switch (extInstance.PathMode) {
					case ExtPathMode.ReachingParkedCar:
					case ExtPathMode.ParkedCarReached:
						ret = Translation.GetString("Entering_vehicle") + ", " + ret;
						break;
					case ExtPathMode.RequiresWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.WalkingToParkedCar:
						ret = Translation.GetString("Walking_to_car") + ", " + ret;
						break;
					case ExtPathMode.PublicTransportToTarget:
						ret = Translation.GetString("Using_public_transport") + ", " + ret;
						break;
					case ExtPathMode.CalculatingWalkingPathToTarget:
					case ExtPathMode.WalkingToTarget:
						ret = Translation.GetString("Walking") + ", " + ret;
						break;
					case ExtPathMode.CalculatingCarPathToTarget:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
						ret = Translation.GetString("Thinking_of_a_good_parking_spot") + ", " + ret;
						break;
				}
			}
			return ret;
		}

		public bool CustomCheckTrafficLights(ushort node, ushort segment) {
			var nodeSimulation = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance().GetNodeSimulation(node) : null;

			var instance = Singleton<NetManager>.instance;
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			var num = (uint)((node << 8) / 32768);
			var stepWaitTime = currentFrameIndex - num & 255u;

			// NON-STOCK CODE START //
			RoadBaseAI.TrafficLightState pedestrianLightState;
			CustomSegmentLights lights = CustomTrafficLightsManager.Instance().GetSegmentLights(node, segment);

			if (lights == null || nodeSimulation == null || !nodeSimulation.IsSimulationActive()) {
				RoadBaseAI.TrafficLightState vehicleLightState;
				bool vehicles;
				bool pedestrians;

				RoadBaseAI.GetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if ((pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed || pedestrianLightState ==  RoadBaseAI.TrafficLightState.Red) && !pedestrians && stepWaitTime >= 196u) {
					RoadBaseAI.SetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, true);
					return true;
				}
			} else {
				if (lights.InvalidPedestrianLight) {
					pedestrianLightState = RoadBaseAI.TrafficLightState.Green;
				} else {
					pedestrianLightState = (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;
				}
			}
			// NON-STOCK CODE END //

			switch (pedestrianLightState) {
				case RoadBaseAI.TrafficLightState.RedToGreen:
					if (stepWaitTime < 60u) {
						return false;
					}
					break;
				case RoadBaseAI.TrafficLightState.Red:
				case RoadBaseAI.TrafficLightState.GreenToRed:
					return false;
			}
			return true;
		}

		private void ArriveAtDestination(ushort instanceID, ref CitizenInstance citizenData, bool success) {
			Log.Error($"HumanAI.ArriveAtDestination is not overriden!");
		}

		private void PathfindFailure(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindFailure is not overriden!");
		}

		private void PathfindSuccess(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindSuccess is not overriden!");
		}

		private void Spawn(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.Spawn is not overriden!");
		}

		private void GetBuildingTargetPosition(ushort instanceID, ref CitizenInstance citizenData, float minSqrDistance) {
			Log.Error($"HumanAI.GetBuildingTargetPosition is not overriden!");
		}
	}
}
