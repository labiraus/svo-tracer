using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Window
{
	public class GraftingData
	{
		public Grafting[] Grafting { get; }
		public Block[] GraftingBlocks { get; }
		public Location[] GraftingAddresses { get; }

		public GraftingData(Grafting[] grafting, Block[] graftingBlocks, Location[] graftingAddresses)
		{
			Grafting = grafting;
			GraftingBlocks = graftingBlocks;
			GraftingAddresses = graftingAddresses;
		}
	}
}
