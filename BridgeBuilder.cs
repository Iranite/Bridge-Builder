using System;
using System.Collections.Generic;
using UnityEngine;



public class BridgeBuilder : MachineEntity, PowerConsumerInterface
{
	public bool Active;

	public float mrCurrentPower = 0.0f;
	public float mrNormalisedPower;
	public float mrPowerSpareCapacity;
	public float PowerPerBuild = 8;
	public float mrMaxPower = 128;
	public int mnMaxSearchDistance = 512;
	public int mnTimeSinceBuild = 0;
	public int BridgeCubeType = 126; // Reinforced Concrete wall
	public int[] GarbageCubes = { 21,68,200,12,16,4,2,3,7,13,24, 199, 87, 17 }; 
	public int GarbageAmount = 8;

	Vector3 mUp;
	Vector3 mForwards;

	public int mnSearchDistance;//once this hits 64, either go to sleep or start again

	Light WorkLight;
	GameObject Builder;

	int mnBuildDelay;

	int mnLFUpdates;

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
	public eCarriedObject mePreviousCarriedObject;
	public bool mbHasHopper;
	public bool Blocked;

	// ************************************************************************************************************************************************
	public BridgeBuilder(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue) : base(eSegmentEntity.Mod, SpawnableObjectEnum.AutoBuilder, x, y, z, cube, flags, lValue, Vector3.zero, segment)
	{
		mbNeedsLowFrequencyUpdate = true;
		mbNeedsUnityUpdate = true;

		mForwards = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.forward;
		mForwards.Normalize();

		mUp = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.up;
		mUp.Normalize();

		mnSearchDistance = 1;

		meCarriedObject = eCarriedObject.eNone;
		mePreviousCarriedObject = meCarriedObject;

		mFlags = flags;

		//mrMaxPower = PowerPerBuild + mnMaxSearchDistance; //but why? (Anm. Iranite)


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

		mnSearchDistance = 1;
	}
	// ************************************************************************************************************************************************

	public override void LowFrequencyUpdate()
	{
		mnTimeSinceBuild++;
		if (mnSearchDistance >= mnMaxSearchDistance) return;//we are done.

		//even if we're carrying an item, ensure the HAS NO HOPPER code is correct
		if (mbHasHopper == false)
		{
			//First of all, collect something from our input hopper
			StorageMachineInterface lHopper = null;
			//search all directions
			for (int i = 0; i < 6; i++)
			{
				lHopper = LookForHopper();
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
				lHopper = LookForHopper();
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
		// {200, 639 }, { 200, 1782 }
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
				lHopper.TryExtractCubes(this, (ushort)200, (ushort)369, GarbageAmount);
				meCarriedObject = eCarriedObject.eConveyor;
			}
			else if (lHopper.CountCubes((ushort)200, (ushort)1782) >= GarbageAmount)
			{
				lHopper.TryExtractCubes(this, (ushort)200, (ushort)1782, GarbageAmount);
				meCarriedObject = eCarriedObject.eConveyor;
			}

			//if (lHopper.TryExtractAny(this, 8, out ItemBase _)) meCarriedObject = eCarriedObject.eConveyor;
			if (meCarriedObject == eCarriedObject.eNone) return;
			if (mePreviousCarriedObject == eCarriedObject.eNone) mePreviousCarriedObject = meCarriedObject;
		}

		//Initially, for testing, only allow conveyors :)

		//If we have enough power, run over our forwards vector until we find a square that's not of the input type
		//may not be needed once we have to get an item first
		if (mnBuildDelay > 0)
		{
			mnBuildDelay--;
			return;
		}

		if (mnSearchDistance > 0)
		{
			if (mrCurrentPower >= PowerPerBuild + mnSearchDistance)
			{
				mrCurrentPower -= PowerPerBuild + mnSearchDistance;
				UpdateSearch();
				mnBuildDelay = 3;//~1s
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

		long checkX = this.mnX;
		long checkY = this.mnY;
		long checkZ = this.mnZ;

		checkX += (long)(mForwards.x * mnSearchDistance);
		checkY += (long)(mForwards.y * mnSearchDistance);
		checkZ += (long)(mForwards.z * mnSearchDistance);

		if (this.mnX == checkX && this.mnY == checkY && this.mnZ == checkZ) Debug.LogError("Error, BridgeBuilder is about to kill itself");


		Segment checkSegment = AttemptGetSegment(checkX, checkY, checkZ);

		if (checkSegment == null)
			return;

		ushort lCube = checkSegment.GetCube(checkX, checkY, checkZ);
		//ushort lValue = checkSegment.GetCubeData(checkX, checkY, checkZ).mValue; // not needed if I only care about the cubeType

		//we need to skip over only the type we have
		// No need to build a bridge if we reached a block other than air.
		if (lCube == BridgeCubeType)
		{
			mnSearchDistance++;// skip over this one, it was already built
			Blocked = false;
		}
		else if (lCube > 1)
		{ 
			// we encountered 'a thing', stop building.
			Blocked = true;
			mnSearchDistance = 0; // SIGNAL AND TRY AGAIN
			mePreviousCarriedObject = eCarriedObject.eNone;
		}
		else//It's not an entity, an object, or ore - DIG THROUGH IT (I suspect that might dig through things like minecart rails of other types tho)
		{
			// there's no need to build air, the build command will happily swap one voxel type for another, even if there's non air.
			WorldScript.instance.BuildOrientationFromEntity(checkSegment, checkX, checkY, checkZ, (ushort)BridgeCubeType, (ushort)1092, mFlags); // 1092 dark gray
			mePreviousCarriedObject = meCarriedObject;
			meCarriedObject = eCarriedObject.eNone;
			mnSearchDistance++;
			mnTimeSinceBuild = 0;
			Blocked = false;
		}
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

		int lnBuilderDistance = mnSearchDistance - 1;
		if (mnSearchDistance == mnMaxSearchDistance) lnBuilderDistance = 0;//return builder to home

		lBuilderTarget.x += (long)(mForwards.x * lnBuilderDistance);
		lBuilderTarget.y += (long)(mForwards.y * lnBuilderDistance);
		lBuilderTarget.z += (long)(mForwards.z * lnBuilderDistance);

		lBuilderTarget += mUp * 0.5f;//so we dock nicely, etc

		Vector3 lMov = (lBuilderTarget - Builder.transform.position);
		if (lMov.sqrMagnitude > 1.0f) lMov.Normalize();

		if (lnBuilderDistance == 0) lMov *= 8.0f;//rewind quickly

		Builder.transform.position += lMov * Time.deltaTime;

		//again, control the light based on distance and things. Players won't be making THAT many of these tho!
		if (mnSearchDistance == mnMaxSearchDistance)
		{
			//we're done, mark as green
			WorkLight.color = Color.Lerp(WorkLight.color, Color.green, 0.1f);
		}
		else
		if (mrCurrentPower < PowerPerBuild)
		{
			WorkLight.color = Color.Lerp(WorkLight.color, Color.red, 0.01f);//lerp to out of power sloooowly
		}
		else
		{
			WorkLight.color = Color.Lerp(WorkLight.color, Color.blue, 0.75f);//lerp to working quickly
		}

	}
	// ************************************************************************************************************************************************
	public override void UnitySuspended()
	{
		Builder = null;
		WorkLight = null;
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
		writer.Write(lrDummy);
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
		mnSearchDistance = 1;//we never really synced up the visuals on this
		reader.ReadBoolean();//IIIIIIIIIIIRELEVANT

		meCarriedObject = (eCarriedObject)reader.ReadInt32();
		mePreviousCarriedObject = meCarriedObject;


		//reader.ReadSingle(); // dummy
		reader.ReadSingle(); // dummy
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
