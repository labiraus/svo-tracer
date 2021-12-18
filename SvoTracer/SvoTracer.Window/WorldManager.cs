using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Window
{
	public class WorldManager
	{
		private readonly object _pruningBufferLock = new();
		private List<Pruning> pruning = new();
		private List<Block> pruningBlockData = new();
		private List<Location> pruningAddresses = new();

		private readonly object _graftingBufferLock = new();
		private List<Grafting> grafting = new();
		private List<Block> graftingBlocks = new();
		private List<Location> graftingAddresses = new();

		public PruningData GetPruningData()
		{

			var pruningArray = Array.Empty<Pruning>();
			var pruningBlockDataArray = Array.Empty<Block>();
			var pruningAddressesArray = Array.Empty<Location>();

			lock (_pruningBufferLock)
			{
				if (pruning.Count == 0) return null;

				pruningArray = pruning.ToArray();
				pruningBlockDataArray = pruningBlockData.ToArray();
				pruningAddressesArray = pruningAddresses.ToArray();

				pruning = new List<Pruning>();
				pruningBlockData = new List<Block>();
				pruningAddresses = new List<Location>();
			}
			return new PruningData(pruningArray, pruningBlockDataArray, pruningAddressesArray);
		}

		public GraftingData GetGraftingData()
		{

			var graftingArray = Array.Empty<Grafting>();
			var graftingBlocksArray = Array.Empty<Block>();
			var graftingAddressesArray = Array.Empty<Location>();

			lock (_graftingBufferLock)
			{
				if (grafting.Count == 0) return null;

				graftingArray = grafting.ToArray();
				graftingBlocksArray = graftingBlocks.ToArray();
				graftingAddressesArray = graftingAddresses.ToArray();

				grafting = new List<Grafting>();
				graftingBlocks = new List<Block>();
				graftingAddresses = new List<Location>();
			}
			return new GraftingData(graftingArray, graftingBlocksArray, graftingAddressesArray);
		}

		public void UpdatePruning(Pruning pruningInput, Block? pruningBlockDataInput, Location? pruningAddressInput)
		{
			lock (_pruningBufferLock)
			{
				if (pruningBlockDataInput != null)
				{
					pruningInput.ColourAddress = (uint)pruningBlockData.Count;
					pruningBlockData.Add(pruningBlockDataInput.Value);
				}
				if (pruningAddressInput != null)
				{
					pruningInput.Address = (uint)pruningAddresses.Count;
					pruningAddresses.Add(pruningAddressInput.Value);
				}
				pruning.Add(pruningInput);
			}
		}

		public void UpdateGrafting(Grafting graftingInput, List<Block> graftingBlockInput, Location? graftingAddressInput)
		{
			lock (_graftingBufferLock)
			{
				graftingInput.GraftDataAddress = (uint)graftingBlocks.Count;
				graftingInput.GraftTotalSize = (uint)graftingBlockInput.Count;
				for (int i = 0; i < graftingBlockInput.Count; i++)
				{
					var graftingBlock = graftingBlockInput[i];
					graftingBlock.Child += graftingInput.GraftDataAddress;
					graftingBlocks.Add(graftingBlock);
				}
				if (graftingAddressInput != null)
				{
					graftingInput.GraftAddress = (uint)graftingAddresses.Count;
					graftingAddresses.Add(graftingAddressInput.Value);
				}
				grafting.Add(graftingInput);
			}
		}
	}
}
