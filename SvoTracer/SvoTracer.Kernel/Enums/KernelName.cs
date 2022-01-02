using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Kernel
{
	public enum KernelName
	{
		Prune,
		Graft,
		Trace,
		Init,
		RunBaseTrace,
		RunBlockTrace,
		EvaluateBackground,
		EvaluateMaterial,
		ResolveRemainders,
		DrawTrace,
	}
}
