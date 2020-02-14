using System;
using System.Collections.Generic;
using UnityEngine;



public class SuperBridgeBuilder : BridgeBuilderBase, PowerConsumerInterface
{
	// ************************************************************************************************************************************************
	public SuperBridgeBuilder(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue) : base(segment, x, y, z, cube, flags, lValue)
	{
		//BridgeCubeType = 145; // Dapper Scale
		GarbageAmount = 1;
		BuilderName = "Super " + BuilderName;

	}

	// ************************************************************************************************************************************************
}