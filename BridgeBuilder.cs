using System;
using System.Collections.Generic;
using UnityEngine;



public class BridgeBuilder : BridgeBuilderBase, PowerConsumerInterface
{
	// ************************************************************************************************************************************************
	public BridgeBuilder(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue) : base(segment, x, y, z, cube, flags, lValue)
	{

	}

	// ************************************************************************************************************************************************
}
