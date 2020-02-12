using System;
using UnityEngine;

public class BridgeBuilderMod : FortressCraftMod
{
	public ushort MyCubeType = ModManager.mModMappings.CubesByKey["iranite.BridgeBuilderCube"].CubeType;
	public ushort MyCubeType2 = ModManager.mModMappings.CubesByKey["iranite.SuperBridgeBuilderCube"].CubeType;
	public override ModRegistrationData Register()
	{
		ModRegistrationData modRegistrationData = new ModRegistrationData();
		modRegistrationData.RegisterEntityHandler("iranite.BridgeBuilderCube");
		modRegistrationData.RegisterEntityHandler("iranite.SuperBridgeBuilderCube");
		Debug.Log("Bridge Builder no. 3");

		//UIManager.NetworkCommandFunctions.Add("iranite.BridgeBuilderInterface", new UIManager.HandleNetworkCommand(BridgeBuilderWindow.HandleNetworkCommand));


		return modRegistrationData;
	}

	public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
	{
		ModCreateSegmentEntityResults result = new ModCreateSegmentEntityResults();

		if (parameters.Cube == MyCubeType)
		{
			parameters.ObjectType = SpawnableObjectEnum.AutoBuilder;
			result.Entity = new BridgeBuilder(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value);
		}
		else if (parameters.Cube == MyCubeType2)
		{
			parameters.ObjectType = SpawnableObjectEnum.AutoBuilder;
			result.Entity = new SuperBridgeBuilder(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value);
		}
		return result;
	}
}