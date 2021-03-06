using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Window
{
	public class PruningData
	{
		public Pruning[] Pruning { get; }
		public Block[] PruningBlockData { get; }
		public Location[] PruningAddresses { get; }

		public PruningData(Pruning[] pruning, Block[] pruningBlockData, Location[] pruningAddresses)
		{
			Pruning = pruning;
			PruningBlockData = pruningBlockData;
			PruningAddresses = pruningAddresses;
		}
	}
}
