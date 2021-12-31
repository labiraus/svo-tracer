using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Kernel
{
	public enum BufferName
	{
		Bases,
		Blocks,
		Usages,
		ChildRequestID,
		ChildRequests,

		Origins,
		FoVs,
		Directions,
		Locations,
		Depths,
		Weightings,
		ParentTraces,
		RootDirections,
		RootLocations,
		RootDepths,
		RootWeightings,
		RootParentTraces,
		BaseTraces,
		BlockTraces,
		BaseTraceQueue,
		BaseTraceQueueID,
		BlockTraceQueue,
		BlockTraceQueueID,
		BackgroundQueue,
		BackgroundQueueID,
		MaterialQueue,
		MaterialQueueID,
		ColourRs,
		ColourGs,
		ColourBs,
		Luminosities,
		RayLengths,
		FinalColourRs,
		FinalColourGs,
		FinalColourBs,
		FinalWeightings,
		AccumulatorID,

		ParentSize,
		ParentResidency,
		Parents,
		DereferenceQueue,
		DereferenceRemaining,
		Semaphor,
		Pruning,
		PruningBlockData,
		PruningAddresses,
		Grafting,
		GraftingBlocks,
		GraftingAddresses,
		HoldingAddresses,
		AddressPosition,
	}
}
