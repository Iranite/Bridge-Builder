using System;
using System.Collections.Generic;
using UnityEngine;
//Q "Extract"
//T "Store"
//E "Interract"
//if (Input.GetButton("Store"));


public class BridgeBuilderBase : MachineEntity, PowerConsumerInterface
{
	public bool Active;

	public float mrCurrentPower = 0.0f;
	public float mrNormalisedPower;
	public float mrPowerSpareCapacity;
	public float PowerPerBuild = 8.0f;
	public float SearchCost;
	public float BuildCost;
	public int mnMaxSearchDistance = 101; //one more, for drone docking reasons...
	public float mrMaxPower;
	public int mnTimeSinceBuild = 0;
	public int BridgeCubeType = 126; // Reinforced Concrete wall
	public int[] GarbageCubes = { 21, 68, 200, 12, 16, 4, 2, 3, 7, 13, 24, 199, 87, 17 };
	public int GarbageAmount = 8;  //1 for Super
	public string BuilderName = "Bridge Builder";

	public Vector3 mUp;
	public Vector3 mForwards;
	public long buildX;
	public long buildY;
	public long buildZ;

	public int mnSearchDistance = 0;//once this hits 64, either go to sleep or start again
	public int BridgeLength;

	public Light WorkLight;
	public GameObject Builder;

	public int mnBuildDelay;

	public int mnLFUpdates;

	//public bool mbHasObject;//conveyor only, initially

	public enum eCarriedObject
	{
		eNone,
		eConveyor,
		eTransportPipe,
		eMinecartTrack,
		ebBasicConveyor,
		eMassStorageCrate,
		eScrapMinecartTrack,
	};
	public eCarriedObject meCarriedObject;
	//public eCarriedObject mePreviousCarriedObject;
	public bool mbHasHopper;
	public bool Blocked;

	// ************************************************************************************************************************************************
	public BridgeBuilderBase(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue) : base(eSegmentEntity.Mod, SpawnableObjectEnum.AutoBuilder, x, y, z, cube, flags, lValue, Vector3.zero, segment)
	{
		mbNeedsLowFrequencyUpdate = true;
		mbNeedsUnityUpdate = true;

		mForwards = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.forward;
		mForwards.Normalize();

		mUp = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.up;
		mUp.Normalize();

		UpdateCost(); //BuildCost and SearchCost get their first value here.

		meCarriedObject = eCarriedObject.eNone;
		mrMaxPower = 3.14159f*BuildCost;
		mFlags = flags;


	}

	// ************************************************************************************************************************************************
	public override void DropGameObject()
	{
		base.DropGameObject();
		mbLinkedToGO = false;
	}
	// ************************************************************************************************************************************************


	public override void OnUpdateRotation(byte newFlags)
	{
		base.OnUpdateRotation(newFlags);
		mFlags = newFlags;

		mForwards = SegmentCustomRenderer.GetRotationQuaternion(mFlags) * Vector3.forward;
		mForwards.Normalize();

		mnSearchDistance = 0;
		UpdateCost();
	}
	// ************************************************************************************************************************************************

	public override void LowFrequencyUpdate()
	{
		mnTimeSinceBuild++;
		if (mnSearchDistance >= mnMaxSearchDistance) //limit reached, stop.
		{
			Blocked = true; //bridge complete!
			mnSearchDistance = 0; //stop
			UpdateCost();
			return;//we are done.
		}
		//even if we're carrying an item, ensure the HAS NO HOPPER code is correct
		if (mbHasHopper == false)
		{
			//First of all, collect something from our input hopper
			StorageMachineInterface lHopper = null;
			//search all directions
			for (int i = 0; i < 6; i++)
			{
				lHopper = LookForHopper(); //find any hopper.
				mnLFUpdates++;
				if (lHopper != null) break;
			}
			if (lHopper != null) mbHasHopper = true;
		}

		if (meCarriedObject == eCarriedObject.eNone)
		{
			mnLFUpdates++;

			//First of all, collect something from our input hopper
			StorageMachineInterface lHopper = null;
			//search all directions
			for (int i = 0; i < 6; i++)
			{
				lHopper = LookForHopper();//find a hopper that isn't empty
				mnLFUpdates++;
				if (lHopper != null) break;
			}

			if (lHopper == null)
			{
				if (mnTimeSinceBuild > 6)//else we go back almost immediately to 'no hopper' display
				{
					mbHasHopper = false;
				}
				//Debug.Log("AB has no hopper!");
				return;
			}
			mbHasHopper = true;
			//check hopper for garbage!
			foreach (int GarbageType in GarbageCubes)
			{
				if (lHopper.CountCubes((ushort)GarbageType, TerrainData.GetDefaultValue((ushort)GarbageType)) >= GarbageAmount)
				{
					lHopper.TryExtractCubes(this, (ushort)GarbageType, TerrainData.GetDefaultValue((ushort)GarbageType), GarbageAmount);
					meCarriedObject = eCarriedObject.eConveyor;
					break;
				}


			}
			// these Cold and Toxic Cavern Stones dont fit into the loop due to not being default colours
			if (lHopper.CountCubes((ushort)200, (ushort)639) >= GarbageAmount)
			{
				lHopper.TryExtractCubes(this, (ushort)200, (ushort)639, GarbageAmount);
				meCarriedObject = eCarriedObject.eConveyor;
			}
			else if (lHopper.CountCubes((ushort)200, (ushort)1782) >= GarbageAmount)
			{
				lHopper.TryExtractCubes(this, (ushort)200, (ushort)1782, GarbageAmount);
				meCarriedObject = eCarriedObject.eConveyor;
			}
			if (meCarriedObject == eCarriedObject.eNone) return;
		}

		//If we have enough power, run over our forwards vector until we find a square that's not of the input type
		//may not be needed once we have to get an item first
		if (mnBuildDelay > 0)
		{
			mnBuildDelay--;
			return;
		}

		if (mnSearchDistance > 0)
		{
			if (mrCurrentPower >= SearchCost)
			{
				mnBuildDelay += 1; //always delay atleast 1
				UpdateSearch();
				
			}
		}

	}
	// ************************************************************************************************************************************************
	StorageMachineInterface LookForHopper()
	{
		long checkX = this.mnX;
		long checkY = this.mnY;
		long checkZ = this.mnZ;

		int lnXMod = 0;
		int lnYMod = 0;
		int lnZMod = 0;

		if (mnLFUpdates % 6 == 0) lnXMod--;
		if (mnLFUpdates % 6 == 1) lnXMod++;
		if (mnLFUpdates % 6 == 2) lnYMod--;
		if (mnLFUpdates % 6 == 3) lnYMod++;
		if (mnLFUpdates % 6 == 4) lnZMod--;
		if (mnLFUpdates % 6 == 5) lnZMod++;

		checkX += lnXMod;
		checkY += lnYMod;
		checkZ += lnZMod;

		Segment checkSegment = AttemptGetSegment(checkX, checkY, checkZ);

		if (checkSegment == null)
			return null;

		StorageMachineInterface storageMachine = checkSegment.SearchEntity(checkX, checkY, checkZ) as StorageMachineInterface;

		if (storageMachine != null)
		{
			//1.19 P3, no longer return invalid hoppers
			eHopperPermissions permissions = storageMachine.GetPermissions();

			if (permissions == eHopperPermissions.AddOnly) return null;
			if (permissions == eHopperPermissions.Locked) return null;
			if (storageMachine.IsEmpty()) return null;

			return storageMachine;
		}

		return null;
	}
	// ************************************************************************************************************************************************
	void UpdateSearch()
	{
		if (meCarriedObject == eCarriedObject.eNone) return;

		//long checkX = this.mnX;
		//long checkY = this.mnY;
		//long checkZ = this.mnZ;

		//BuildLoc = this.mPosition + mForwards*mnSearchDistance;

		if (buildX == this.mnX && buildY == this.mnY && buildZ == this.mnZ) Debug.LogError("Error, BridgeBuilder is about to kill itself");


		Segment checkSegment = AttemptGetSegment(buildX, buildY, buildZ);

		if (checkSegment == null) return;

		ushort lCube = checkSegment.GetCube(buildX, buildY, buildZ);
		//ushort lValue = checkSegment.GetCubeData(buildX, buildY, buildZ).mValue; // not needed if I only care about the cubeType

		//we need to skip over decorational blocks - makes it easier ;-)
		// No need to build a bridge if we reached a block other than air.
		if (CubeHelper.GetCategory(lCube) == MaterialCategories.Decoration) 
		{
			mnSearchDistance++;// skip over this one, it was already built
			UpdateCost();
			mrCurrentPower -= SearchCost; //searching costs power
			Blocked = false;
		}
		else if (lCube > 1) //We only build in air
		{
			// we encountered 'a thing', stop building.
			Blocked = true;
			mnSearchDistance = 0; // signal to stop working
			UpdateCost();
		}
		else if(mrCurrentPower >= BuildCost)
		{
			// there's no need to build air, the build command will happily swap one voxel type for another, even if there's non air.
			WorldScript.instance.BuildOrientationFromEntity(checkSegment, buildX, buildY, buildZ, (ushort)BridgeCubeType, (ushort)1092, mFlags); // 1092 dark gray
			mrCurrentPower -= BuildCost; //building costs more power.
			meCarriedObject = eCarriedObject.eNone;
			mnSearchDistance++;
			UpdateCost();
			mnBuildDelay += 2;
			mnTimeSinceBuild = 0;
			Blocked = false;
		}
	}
	// ************************************************************************************************************************************************
	public void UpdateCost()
	{
		int FZero = 0;
		if (mnSearchDistance == 0) FZero = 1; //offset to park the Drone on the machine or not
		else BridgeLength = mnSearchDistance-1; // for the UI
		SearchCost = (float)Math.Sqrt(mnSearchDistance)+FZero;
		BuildCost = PowerPerBuild + (float)Math.Pow(mnSearchDistance, 0.65)+FZero;
		mrMaxPower = 3.14159f*BuildCost; //I chose Pi because I'm a nerd
		buildX = this.mnX + (long)(mForwards.x * mnSearchDistance);
		buildY = this.mnY + (long)(mForwards.y * mnSearchDistance);
		buildZ = this.mnZ + (long)(mForwards.z * mnSearchDistance);
	}
	// ************************************************************************************************************************************************
	bool mbLinkedToGO;
	public override void UnityUpdate()
	{
		if (!mbLinkedToGO)
		{
			if (mWrapper == null || mWrapper.mbHasGameObject == false)
			{
				return;
			}
			else
			{
				Builder = mWrapper.mGameObjectList[0].gameObject.transform.Search("Builder").gameObject;
				WorkLight = mWrapper.mGameObjectList[0].gameObject.transform.Search("WorkLight").gameObject.GetComponent<Light>();
				mbLinkedToGO = true;
			}

		}

		Vector3 lBuilderTarget = mWrapper.mGameObjectList[0].gameObject.transform.position;
		int lnBuilderDistance;
		if (mnSearchDistance == 1) lnBuilderDistance = 0;
		else lnBuilderDistance = mnSearchDistance;
		//if (mnSearchDistance == 0) lnBuilderDistance = 0;//return builder to home

		lBuilderTarget.x += (long)(mForwards.x * lnBuilderDistance);
		lBuilderTarget.y += (long)(mForwards.y * lnBuilderDistance);
		lBuilderTarget.z += (long)(mForwards.z * lnBuilderDistance);

		lBuilderTarget += mUp * 0.75f;//so we dock nicely, etc    ----------- This was 0.5f - changed it so it can hover above a full block.

		Vector3 lMov = (lBuilderTarget - Builder.transform.position);
		//if (lMov.sqrMagnitude > 1.5f) lMov.Normalize()*1.5f;  //used to be normalized if it gets greater than 1, i did this so it can be faster.
		//if(lnBuilderDistance == 0) Vector3.ClampMagnitude(lMov, 8.0f);// Don't rewind too quickly ------ not needed if I allow him to go faster.
		
		Builder.transform.position += lMov * Time.deltaTime;

		//again, control the light based on distance and things. Players won't be making THAT many of these tho!
		if (mnSearchDistance == 0)
		{
			//we're done, mark as green
			WorkLight.color = Color.Lerp(WorkLight.color, Color.green, 0.1f);
		}
		else if (mrCurrentPower < BuildCost|| meCarriedObject == eCarriedObject.eNone)
		{
			WorkLight.color = Color.Lerp(WorkLight.color, Color.red, 0.01f);//lerp to out of power sloooowly
		}
		else
		{
			WorkLight.color = Color.Lerp(WorkLight.color, Color.blue, 0.75f);//lerp to working quickly
		}

	}
	public override void UnitySuspended()
	{
		Builder = null;
		WorkLight = null;
	}
	// ************************************************************************************************************************************************

	// The intended Format for the UI: x means will always be there! How many lines are possible again?
	//1x	Bridge Builder
	//2x	Power: int/INT - Low Power!
	//3x	Power cost to Build: int, to Search: int.
	//4x	No valid Hopper! / Needs more Garbage! / All Ready!, Low Power!
	//5		Bridge complete!
	//6x	Waiting for orders... / Build distance is: / was: int
	//7x	(E) to start/reset
	//8x	Build limit: INT
	//9x	(Q): -int, (T): +int - (Shift/Ctrl)
	public override string GetPopupText()
	{
		string lStr = BuilderName;
		lStr += "\n" + string.Format(PersistentSettings.GetString("Power_X_X"), mrCurrentPower.ToString("F0"), mrMaxPower.ToString("F0"));
		if (mrCurrentPower < SearchCost)
		{
			//float lrmissing = PowerPerBuild - mrCurrentPower;
			//lStr += "\n" + string.Format(PersistentSettings.GetString("UI_Needs_X_more_power"), lrmissing.ToString("F0"));
			lStr += " - Low Power!";
		}
		lStr += "\nPower cost to Build: " + (BuildCost).ToString("F0") + ", to Search: " + SearchCost.ToString("F0");
		//	lStr +=  "\nC:" + meCarriedObject.ToString() +":P"+ mePreviousCarriedObject.ToString();
		//if (lStat.mrCurrentPower < lStat.mrMaxPower * 0.25f) lStr +=  "\nLow on power!";
		if (mbHasHopper == false)
		{
			lStr += "\nNo valid Hopper!";
		}
		else if (meCarriedObject == eCarriedObject.eNone && mnTimeSinceBuild > 6)
		{
			lStr += "\nNeeds more Garbage!";
		}
		else if (mrCurrentPower >= BuildCost)
		{
			//Have hopper, am building
			lStr += "\nAll Ready!";
		}
		else
			lStr += "\nBuilder low on Power!";
		if (Blocked)
		{
			lStr += "\nBridge complete!"; //The other side has been reached (Blocked)
			lStr += "\nBuild Distance was: " + (BridgeLength).ToString("F0");
		}
		if (mnSearchDistance > 0)
		{
			lStr += "\nBuild Distance is: " + (BridgeLength).ToString("F0");
			lStr += "\n(E) Reset!"; //Interaction
			if (Input.GetButtonDown("Interact"))
			{
				mnSearchDistance = 0;
				//BridgeLength = 0; //why did I have this here again?
				Blocked = false;
				UpdateCost();
					
			}
		}
		else
		{
			if(!Blocked)lStr += "\nWaiting for orders...";
			lStr += "\n(E) Start!"; //Interaction
			if (Input.GetButtonDown("Interact"))
			{
				mnSearchDistance = 1;
				Blocked = false;
				UpdateCost();
			}
		}
		int changeDist = 100;
		if (Input.GetKey(KeyCode.LeftShift))
			changeDist = 10;
		else if (Input.GetKey(KeyCode.LeftControl))
			changeDist = 1;
		lStr += "\nBuild limit: " + (mnMaxSearchDistance-1).ToString("F0") + "\n(Q): -" + (changeDist).ToString("F0")+ ", (T): +" + (changeDist).ToString("F0") + " - (Shift/Ctrl)"; //Interaction
		if (Input.GetButtonDown("Store"))
			mnMaxSearchDistance += changeDist;
		if (Input.GetButtonDown("Extract"))
		{
			mnMaxSearchDistance -= changeDist;
			if (mnMaxSearchDistance < 2) mnMaxSearchDistance = 2;
		}

		return lStr;
	}
	//******************** PowerConsumerInterface **********************
	public float GetRemainingPowerCapacity()
	{
		return mrMaxPower - mrCurrentPower;
	}

	public float GetMaximumDeliveryRate()
	{
		return float.MaxValue; // we're limited by how fast other machines can pump into us, nothing else 
	}

	public float GetMaxPower()
	{
		return mrMaxPower;
	}

	public bool WantsPowerFromEntity(SegmentEntity entity)
	{
		return true;
	}

	public bool DeliverPower(float amount)
	{
		if (amount > GetRemainingPowerCapacity())
			return false;

		mrCurrentPower += amount;
		MarkDirtyDelayed();
		return true;
	}
	// ************************************************************************************************************************************************
	public override bool ShouldSave()
	{
		return true;
	}

	// ************************************************************************************************************************************************
	public override void Write(System.IO.BinaryWriter writer)
	{
		writer.Write(mnSearchDistance);
		writer.Write(false);
		float lrDummy = 0;

		//writer.Write(lrDummy);
		writer.Write((int)meCarriedObject);
		writer.Write(mnMaxSearchDistance);
		writer.Write(lrDummy);
		writer.Write(lrDummy);

		writer.Write(lrDummy);
		writer.Write(lrDummy);
		writer.Write(lrDummy);
		writer.Write(lrDummy);

	}
	// ************************************************************************************************************************************************
	//bool mbJustLoaded;
	public override void Read(System.IO.BinaryReader reader, int entityVersion)
	{

		mnSearchDistance = reader.ReadInt32();
		//mnSearchDistance = 1;//we never really synced up the visuals on this ---------- I don't care, I changed the visuals anyway The drone will now quickly move into position.
		reader.ReadBoolean();//IIIIIIIIIIIRELEVANT

		//reader.ReadSingle(); // dummy
		meCarriedObject = (eCarriedObject)reader.ReadInt32();
		mnMaxSearchDistance = reader.ReadInt32();
		reader.ReadSingle(); // dummy
		reader.ReadSingle(); // dummy

		reader.ReadSingle(); // dummy
		reader.ReadSingle(); // dummy
		reader.ReadSingle(); // dummy
		reader.ReadSingle(); // dummy
	}
	// ************************************************************************************************************************************************

	//******************** Holobase **********************

	/// <summary>
	/// Called when the holobase has been opened and it requires this entity to add its
	/// visualisations. If there is no visualisation for an entity return null.
	/// 
	/// To receive updates each frame set the <see cref="HoloMachineEntity.RequiresUpdates"/> flag.
	/// </summary>
	/// <returns>The holobase entity visualisation.</returns>
	/// <param name="holobase">Holobase.</param>
	public override HoloMachineEntity CreateHolobaseEntity(Holobase holobase)
	{
		var creationParameters = new HolobaseEntityCreationParameters(this);

		creationParameters.AddVisualisation(holobase.mPreviewCube);

		return holobase.CreateHolobaseEntity(creationParameters);
	}

	// ************************************************************************************************************************************************
}
