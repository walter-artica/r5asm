using System;
using System.Collections.Generic;

namespace r5sim
{
	class Program
	{
		static int Main(string[] args)
		{
			int retval = 0;

			if (args.Length < 1)
				PrintLogo();
			else
			{
				string fn = args[0];
				string[] pargs = new string[args.Length-1];
				Array.Copy(args, 1, pargs, 0, args.Length-1);
				try
				{
					Simulator sim = new Simulator(fn, pargs);
					sim.Execute();
				}
				catch (Exception e)
				{
					retval = (int)e.Data[0];
				}
			}
#if DEBUG
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
#endif
			return retval;
		}

		static void PrintLogo()
		{
			Console.WriteLine("RISC-5 Simulator -- Copyright(c) 2015\n\n" +
							  "\tr5sim filename.[txt|bin] [program_arguments]\n");
		}
	}
}
