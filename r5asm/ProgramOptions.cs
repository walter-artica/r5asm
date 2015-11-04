using System;
using System.Collections.Generic;
using System.Text;

namespace r5asm
{
	sealed class ProgramOptions
	{
		public string outputName;
		public OutputType outtype;
		public bool useGPbased, enableListing, useAT;

		// Default options
		public ProgramOptions()
		{
			outputName = null;
			outtype = OutputType.Text;
			useGPbased = true;
			enableListing = false;
			useAT = true;
		}
	}
}
