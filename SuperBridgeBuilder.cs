using System;
using System.Collections.Generic;
using UnityEngine;



public class SuperBridgeBuilder : BridgeBuilder, PowerConsumerInterface
{
	// ************************************************************************************************************************************************
	public SuperBridgeBuilder(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue) : base(segment, x, y, z, cube, flags, lValue)
	{
		//BridgeCubeType = 145; // Dapper Scale
		GarbageAmount = 1;
		GarbageYield = 2; //2 for Super
		BuilderName = "Super " + BuilderName;

	}

	// ************************************************************************************************************************************************
}