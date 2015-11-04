/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 29/01/2012
 * Time: 04:48 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace r5asm
{
	sealed class Assembler
	{
		Scanner scanner;
		SymbolTable symtab;
		Parser parser;
		CodeGen codegen;
		string outname;
		bool genlist;
		System.IO.StreamWriter listwriter;

		public Assembler(ProgramOptions options)
		{
			genlist = options.enableListing;
			outname = options.outputName;
			if (genlist)
			{
				listwriter = new System.IO.StreamWriter(new System.IO.MemoryStream());
			}
			scanner = new Scanner();
			symtab = new SymbolTable(scanner);
			codegen = new CodeGen(scanner, symtab, options, listwriter);
			parser = new Parser(scanner, codegen, symtab, options, listwriter);
		}
		
		void Pass1()
		{
			parser.Init(1);
			parser.Parse();
		}
		
		void Pass2()
		{
			parser.Init(2);
			parser.Parse();
		}
		
		public void AssembleUnit(string fname)
		{
			scanner.Init(fname);
			Console.WriteLine(" Assembling: {0}", scanner.ModFilename);
			Pass1();
			if (scanner.ErrorCount != 0) return;
			symtab.CheckForMissingExports();
			if (scanner.ErrorCount != 0) return;
			codegen.Rebase();
			if (genlist)
			{
				listwriter.WriteLine("FILE: {0}", fname);
				listwriter.WriteLine("DATE: {0}", DateTime.Now);
				listwriter.WriteLine("\n** CODE section **\n");
			}
			Pass2();
			if (scanner.ErrorCount != 0) return;
			codegen.WriteOutputFile(outname);
			if (genlist)
			{
				symtab.ListSymbols(listwriter);
				string lname = System.IO.Path.GetFileNameWithoutExtension(fname);
				WriteListingFile(lname + ".lst");
				listwriter.Close();
			}
		}
		
		void WriteListingFile(string lname)
		{
			listwriter.Flush();
			System.IO.MemoryStream ms = listwriter.BaseStream as System.IO.MemoryStream;
			try
			{
				System.IO.FileStream fs = new System.IO.FileStream(lname, System.IO.FileMode.Create);
				ms.WriteTo(fs);
			}
			catch
			{
				Console.WriteLine("FATAL: Cannot write to listing file '{0}'", lname);
			}
		}
	}
}
