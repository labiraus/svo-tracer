using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Kernel
{
	public enum BufferName
	{
		BaseBlocks,
		Blocks,
		Usage,
		ChildRequestId,
		ChildRequests,
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
