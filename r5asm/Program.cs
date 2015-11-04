/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 29/01/2012
 * Time: 03:44 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;

namespace r5asm
{
	sealed class Program
	{
		enum ArgType { String, Option }

		sealed class Argument
		{
			public ArgType type;
			public string str;
			public string param;

			public Argument(ArgType type, string str)
			{
				this.type = type;
				this.str = str;
				this.param = null;
			}
		}

		static bool hasErrors = false;
		static List<Argument> arglist;

		public static void Main(string[] args)
		{
			if (args.Length < 1)
				PrintLogo();
			else
				Do(args);
#if DEBUG
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
#endif
		}

		static void PrintLogo()
		{
			const int LEN = 12;

			Console.WriteLine("RISC Assembler -- Copyright(c) 2012\n\n" +
							  "\tRAS [/options] filelist [/options]\n");
			Console.WriteLine("{0}{1}", "/o:<name>".PadRight(LEN), "Output filename");
			Console.WriteLine("{0}{1}", "/f:bin".PadRight(LEN), "Output format = raw binary");
			Console.WriteLine("{0}{1}", "/f:txt".PadRight(LEN), "Output format = text");
			Console.WriteLine("{0}{1}", "/f:hex".PadRight(LEN), "Output format = Intel Hex");
			Console.WriteLine("{0}{1}", "/f:elf".PadRight(LEN), "Output format = ELF (default)");
			Console.WriteLine("{0}{1}", "/l".PadRight(LEN), "Generate listing");
			Console.WriteLine("{0}{1}", "/nogp".PadRight(LEN), "Don't use GP-based addressing");
			Console.WriteLine("{0}{1}", "/noat".PadRight(LEN), "Don't use the AT register");
		}

		static void PrintError(string msg)
		{
			Console.WriteLine("Error: " + msg);
			hasErrors = true;
		}

		static void Do(string[] args)
		{
			string fname;
			Assembler assembler;
			ProgramOptions opts = new ProgramOptions();

			DissectInput(args);
#if DEBUG
			//PrintDissectedInput();
#endif
			if (hasErrors) return;
			ParseInput(opts);
			if (hasErrors) return;

			foreach (Argument a in arglist)
			{
				if (a.type == ArgType.String)
				{
					fname = a.str;
					if (!System.IO.File.Exists(fname))
					{
						PrintError("FATAL: File '" + fname + "' does not exist");
						return;
					}
					assembler = new Assembler(opts);
					assembler.AssembleUnit(fname);
				}
			}
		}

		static void DissectInput(string[] args)
		{
			int i;
			Argument a;
			string s;

			arglist = new List<Program.Argument>();
			for (i = 0; i < args.Length; i++)
			{
				s = args[i];
				if (s[0] == '/')
				{
					if (s.Length < 2)
					{
						PrintError("Option expected after '/'");
						return;
					}
					a = new Argument(ArgType.Option, s[1].ToString());
					if (s.Length >= 3)
					{
						if (s[2] != ':')
						{
							PrintError("Invalid option format");
							return;
						}
						if (s.Length < 4)
						{
							PrintError("Option parameter expected after ':'");
							return;
						}
						a.param = s.Substring(3);
					}
					arglist.Add(a);
				}
				else
				{
					arglist.Add(new Argument(ArgType.String, s));
				}
			}
		}

		static void ParseInput(ProgramOptions options)
		{
			int fncount;

			fncount = 0;
			foreach (Argument a in arglist)
			{
				if (a.type == ArgType.Option)
				{
					switch (a.str)
					{
						case "o":
							if (a.param == null)
							{
								PrintError("Output filename expected");
								return;
							}
							options.outputName = a.param;
							break;
						case "f":
							switch (a.param)
							{
								case "bin":
									options.outtype = OutputType.Raw; break;
								case "txt":
									options.outtype = OutputType.Text; break;
								case "hex":
									options.outtype = OutputType.Hex; break;
								case "elf":
									options.outtype = OutputType.Elf; break;
								case null:
									PrintError("Output format expected");
									return;
								default:
									PrintError("Unknown output format: '" + a.param + "'");
									return;
							}
							break;
						case "l":
							options.enableListing = true;
							break;
						case "nogp":
							options.useGPbased = false;
							break;
						case "noat":
							options.useAT = false;
							break;
						default:
							PrintError("Unknown option: '" + a.str + "'");
							return;
					}
				}
				else if (a.type == ArgType.String)
				{
					++fncount;
				}
			}

			if (fncount == 0)
			{
				PrintError("No input files specified");
			}
			else if (fncount > 1 && options.outputName != null)
			{
				PrintError("Output filename can't be specified if there are multiple input files");
			}
		}

		static void PrintDissectedInput()
		{
			int i;

			i = 0;
			Console.WriteLine("Input arguments:\n");
			foreach (Argument a in arglist)
			{
				if (a.type == ArgType.String)
					Console.WriteLine("[{0}] {1}", i, a.str);
				else
					Console.WriteLine("[{0}] /{1}{2}{3}", i, a.str, a.param != null ? ":" : "", a.param);
				i++;
			}
			Console.WriteLine();
		}
	}
}