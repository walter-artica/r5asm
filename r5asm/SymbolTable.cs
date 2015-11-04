/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 30/01/2012
 * Time: 10:10 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;

namespace r5asm
{
	enum SymbolType
	{
		Null, Label, Constant, Pending, Import
	}
	
	class Symbol
	{
		public Symbol(SymbolType symt, string name, uint val, SectionType sect)
		{
			this.type = symt;
			this.name = name;
			this.val = val;
			this.sectType = sect;
			this.exported = false;
			this.index = 0;
			this.strindex = 0;
		}
		
		public Symbol(SymbolType symt, string name, uint val)
		{
			this.type = symt;
			this.name = name;
			this.val = val;
			this.sectType = SectionType.Null;
			this.exported = false;
			this.index = 0;
			this.strindex = 0;
		}
		
		public SymbolType type;
		public string name;
		public uint val;
		public SectionType sectType;
		public bool exported;
		public uint index;
		public uint strindex;
	}
	
	/// <summary>
	/// Description of SymbolTable.
	/// </summary>
	sealed class SymbolTable
	{
		Dictionary<string, Symbol> tab;
		List<Symbol> filetab;
		Scanner scanner;
		
		public SymbolTable(Scanner scanner)
		{
			tab = new Dictionary<string, Symbol>();
			this.scanner = scanner;
		}
		
		public List<Symbol> FileTable
		{
			get
			{
				return filetab;
			}
		}
		
		public void InsertConstant(string name, uint val)
		{
			if (tab.ContainsKey(name))
				scanner.Mark(ErrorType.DuplicatedDefinition);
			else
				tab.Add(name, new Symbol(SymbolType.Constant, name, val));
		}
		
		public void InsertConstant(string name, int val)
		{
			unchecked
			{
				InsertConstant(name, (uint)val);
			}
		}
		
		public Symbol InsertLabel(string name, uint val, SectionType st)
		{
			Symbol s;
			
			if (tab.ContainsKey(name))
			{
				s = tab[name];
				if (s.type == SymbolType.Pending)
				{
					s.type = SymbolType.Label;
					s.sectType = st;
					s.val = val;
				}
				else
				{
					s = null;
					scanner.Mark(ErrorType.DuplicatedDefinition);
				}
			}
			else
			{
				s = new Symbol(SymbolType.Label, name, val, st);
				tab.Add(name, s);
			}
			return s;
		}
		
		public void InsertLabel(string name, int val, SectionType st)
		{
			unchecked
			{
				InsertLabel(name, (uint)val, st);
			}
		}
		
		public void InsertExport(string name)
		{
			Symbol s;
			
			if (tab.ContainsKey(name))
			{
				scanner.Mark(ErrorType.DuplicatedDefinition);
			}
			else
			{
				s = new Symbol(SymbolType.Pending, name, 0, SectionType.Null);
				s.exported = true;
				tab.Add(name, s);
			}
		}
		
		public void InsertImport(string name)
		{
			Symbol s;
			
			if (tab.ContainsKey(name))
			{
				scanner.Mark(ErrorType.DuplicatedDefinition);
			}
			else
			{
				s = new Symbol(SymbolType.Import, name, 0, SectionType.Null);
				tab.Add(name, s);
			}
		}
		
		/*
		public bool Has(string name)
		{
			return tab.ContainsKey(name);
		}
		*/
		
		public Symbol Find(string name)
		{
			if (tab.ContainsKey(name))
			{
				return tab[name];
			}
			else
			{
				return null;
			}
		}
		
		public void RebaseSection(SectionType stype, uint b)
		{
			foreach (Symbol sym in tab.Values)
			{
				if (sym.type == SymbolType.Label && sym.sectType == stype)
				{
					sym.val += b;
				}
			}
		}
		
		public void CheckForMissingExports()
		{
			foreach (Symbol sym in tab.Values)
			{
				if (sym.type == SymbolType.Pending)
				{
					scanner.MarkExport(sym.name);
				}
			}
		}
		
		public uint BuildFileSymbolTable(out string stringdata)
		{
			System.Text.StringBuilder sb;
			
			sb = new System.Text.StringBuilder();
			filetab = new List<Symbol>();
			filetab.Add(new Symbol(SymbolType.Import, "", 0, SectionType.Null));
			foreach (Symbol sym in tab.Values)
			{
				if (sym.type == SymbolType.Import || sym.exported == true)
				{
					filetab.Add(sym);
				}
			}
			if (filetab.Count == 1) filetab.Clear();
			else
			{
				for (int i = 1; i < filetab.Count; i++)
				{
					filetab[i].strindex = (uint)sb.Length;
					filetab[i].index = (uint)i;
					sb.Append(filetab[i].name);
					sb.Append('\0');
				}
			}
			stringdata = sb.ToString();
			return (uint)filetab.Count;
		}
		
		public void ListSymbols(System.IO.StreamWriter w)
		{
			w.WriteLine("\n** DATA section **\n");
			foreach (Symbol sym in tab.Values)
			{
				if (sym.type == SymbolType.Label && sym.sectType == SectionType.InitializedData)
				{
					w.WriteLine("{0:X8} {1}", sym.val, sym.name);
				}
			}
			w.WriteLine("\n** BSS section **\n");
			foreach (Symbol sym in tab.Values)
			{
				if (sym.type == SymbolType.Label && sym.sectType == SectionType.UninitializedData)
				{
					w.WriteLine("{0:X8} {1}", sym.val, sym.name);
				}
			}
		}
	}
}
