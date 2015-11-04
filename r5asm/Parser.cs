/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 29/01/2012
 * Time: 04:34 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace r5asm
{
	sealed class Parser
	{
		SymbolTable symtab;
		Scanner scanner;
		CodeGen codegen;
		Token sym;
		int passnum;
		List<Symbol> labelList_ID, labelList_UD;
		bool genlist;
		System.IO.StreamWriter listwriter;

		public Parser(Scanner scanner, CodeGen codegen, SymbolTable symtab, ProgramOptions options,
					  System.IO.StreamWriter listingWriter)
		{
			this.scanner = scanner;
			this.codegen = codegen;
			this.symtab = symtab;
			this.genlist = options.enableListing;
			this.listwriter = listingWriter;
			labelList_ID = new List<Symbol>();
			labelList_UD = new List<Symbol>();
		}
		
		public void Init(int passnum)
		{
			Debug.Assert(passnum == 1 || passnum == 2, "Illegal pass number");
			this.passnum = passnum;
			scanner.Reset();
			codegen.Init();
			labelList_ID.Clear();
			labelList_UD.Clear();
		}

		bool IsStartOfExpression(Token tok)
		{
			return tok == Token.Number
				|| tok == Token.Ident
				|| tok == Token.Plus
				|| tok == Token.Minus
				|| tok == Token.Lparen;
		}
		
		int GetFactor(out Symbol x)
		{
			int n;
			uint w;
			Symbol y;
			string id;
			
			x = null;
			if (sym == Token.Not)
			{
				scanner.Get(out sym);
				n = ~GetFactor(out x);
			}
			else if (sym == Token.Number)
			{
				unchecked { n = (int)scanner.Number; }
				scanner.Get(out sym);
			}
			else if (sym == Token.Ident)
			{
				id = scanner.Name.ToUpper();
				if (id == "LO" || id == "HI")
				{
					scanner.Get(out sym);
					if (sym != Token.Lparen)
					{
						scanner.Mark(ErrorType.LeftParenExpected);
						n = 0;
					}
					scanner.Get(out sym);
					w = (uint)GetExpression(out y);
					if (sym != Token.Rparen)
						scanner.Mark(ErrorType.RightParenExpected);
					else
						scanner.Get(out sym);
					if (y != null)
						scanner.Mark(ErrorType.InvalidOperationForRelocatableSymbol);
					if (id == "HI")
						n = (int)CodeGen.GetHighPart(w);
					else
						n = (int)CodeGen.GetLowPart(w);
				}
				else
				{
					y = symtab.Find(scanner.Name);
					if (y == null)
					{
						scanner.Mark(ErrorType.UndefinedIdentifier);
						n = 0;
					}
					else
					{
						if ((y.type == SymbolType.Label || y.type == SymbolType.Import))
						{
							x = y;
						}
						scanner.Get(out sym);
						n = (int)y.val;
					}
				}
			}
			else if (sym == Token.Lparen)
			{
				scanner.Get(out sym);
				n = GetExpression(out y);
				if (sym != Token.Rparen)
					scanner.Mark(ErrorType.RightParenExpected);
				else
					scanner.Get(out sym);
			}
			else
			{
				scanner.Mark(ErrorType.IllegalExpression);
				n = 0;
			}
			
			return n;
		}
		
		int GetTerm(out Symbol x)
		{
			Token op;
			int n;
			Symbol y;
			
			n = GetFactor(out x);
			while (sym == Token.Times || sym == Token.Slash || sym == Token.And)
			{
				op = sym;
				scanner.Get(out sym);
				if (op == Token.Times)
				{
					n *= GetFactor(out y);
				}
				else if (op == Token.Slash)
				{
					n /= GetFactor(out y);
				}
				else
				{
					n &= GetFactor(out y);
				}
				if (x != null || y != null)
					scanner.Mark(ErrorType.InvalidOperationForRelocatableSymbol);
			}
			
			return n;
		}
		
		int GetExpression(out Symbol x)
		{
			bool neg;
			Token op;
			int n;
			Symbol y;
			
			neg = false;
			if (sym == Token.Plus)
				scanner.Get(out sym);
			else if (sym == Token.Minus)
			{
				neg = true;
				scanner.Get(out sym);
			}
			n = GetTerm(out x);
			if (neg)
			{
				n = -n;
				if (x != null)
					scanner.Mark(ErrorType.InvalidOperationForRelocatableSymbol);
			}
			while (sym == Token.Plus || sym == Token.Minus || sym == Token.Or)
			{
				op = sym;
				scanner.Get(out sym);
				if (op == Token.Plus)
				{
					n += GetTerm(out y);
					if (x == null)
						x = y;
					else if (y != null)
						scanner.Mark(ErrorType.InvalidOperationBetweenRelocatableSymbols);
				}
				else if (op == Token.Minus)
				{
					n -= GetTerm(out y);
					if (y != null)
					{
						if (x == null)
							scanner.Mark(ErrorType.InvalidOperationForRelocatableSymbol);
						else if (x.type == SymbolType.Import || y.type == SymbolType.Import)
							scanner.Mark(ErrorType.InvalidOperationBetweenRelocatableSymbols);
						else
							x = null;	
					}
				}
				else
				{
					n |= GetTerm(out y);
					if (x != null || y != null)
						scanner.Mark(ErrorType.InvalidOperationForRelocatableSymbol);
				}
			}
			
			return n;
		}
		
		void AddToLabelList(Symbol s)
		{
			if (codegen.CurrentSection == SectionType.InitializedData)
			{
				labelList_ID.Add(s);
			}
			else if (codegen.CurrentSection == SectionType.UninitializedData)
			{
				labelList_UD.Add(s);
			}
		}
		
		void GetLabel()
		{
			string id;
			Symbol s;
			
			if (sym == Token.Ident)
			{
				id = scanner.Name;
				scanner.Get(out sym);
				if (sym != Token.Colon)
				{
					scanner.Mark(ErrorType.UnknownIdentifier);
					return;
				}
				scanner.Get(out sym);
				if (passnum == 1)
				{
					if (codegen.CurrentSection == SectionType.Null)
						scanner.Mark(ErrorType.OutsideSection);
					else
					{
						s = symtab.InsertLabel(id, codegen.PC, codegen.CurrentSection);
						AddToLabelList(s);
					}
				}
			}
		}

		void GetDataReservation(string type)
		{
			int count;
			uint newaddr = 0;
			bool isPass1;
			Symbol refsym;
			
			isPass1 = passnum == 1;
			
			if (codegen.CurrentSection == SectionType.Null)
				scanner.Mark(ErrorType.OutsideSection);
			else if (codegen.CurrentSection == SectionType.Code && type != "resw")
			{
				scanner.Mark(ErrorType.OnlyWordsInCode);
				return;
			}
			
			count = GetExpression(out refsym);
			if (refsym != null)
			{
				scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
			}
			if (count < 1)
			{
				scanner.Mark(ErrorType.CountMustBePositive);
				count = 1;
			}
			
			if (type == "resb")
			{
				codegen.PutByte(0, count, out newaddr, !isPass1);
			}
			else if (type == "resh")
			{
				codegen.PutHalfWord(0, count, out newaddr, !isPass1);
			}
			else
			{
				if (codegen.CurrentSection == SectionType.Code)
					codegen.ReserveCodeWord(count, !isPass1);
				else
					codegen.PutWord(0, count, out newaddr, !isPass1);
			}
			
			if (isPass1)
			{
				if (codegen.CurrentSection == SectionType.InitializedData)
				{
					foreach (Symbol s in labelList_ID)
					{
						s.val = newaddr;
					}
					labelList_ID.Clear();
				}
				else if (codegen.CurrentSection == SectionType.UninitializedData)
				{
					foreach (Symbol s in labelList_UD)
					{
						s.val = newaddr;
					}
					labelList_UD.Clear();
				}
			}
		}
		
		void GetDataInitializer(string type)
		{
			int val;
			uint newaddr = 0;
			bool isPass1;
			bool hasInitializer;
			bool isCodeSection, isIDataSection, isUDataSection;
			bool isTypeWord, isTypeHalf, isTypeByte;
			string str;
			Symbol refsym;
			
			str = null;
			hasInitializer = false;
			isPass1 = passnum == 1;
			isCodeSection = codegen.CurrentSection == SectionType.Code;
			isIDataSection = codegen.CurrentSection == SectionType.InitializedData;
			isUDataSection = codegen.CurrentSection == SectionType.UninitializedData;
			isTypeWord = type == "word";
			isTypeHalf = type == "hword";
			isTypeByte = type == "byte";
			
			val = 0;
			if (IsStartOfExpression(sym))
			{
				hasInitializer = true;
				val = GetExpression(out refsym);
				if (refsym != null)
					scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
			}
			else if (isTypeByte && sym == Token.String)
			{
				hasInitializer = true;
				str = scanner.Name;
				scanner.Get(out sym);
			}
			else if (isCodeSection)
			{
				codegen.PutCodeWord(!isPass1);
				return;
			}
			
			// Abnormal conditions
			if (codegen.CurrentSection == SectionType.Null)
			{
				scanner.Mark(ErrorType.OutsideSection);
				return;
			}
			else if (isCodeSection && !isTypeWord)
			{
				scanner.Mark(ErrorType.OnlyWordsInCode);
				return;
			}
			else if (isUDataSection && hasInitializer)
			{
				scanner.Mark(ErrorType.InitializedDataNotSupported);
				return;
			}
			
			// Put actual data
			if (isTypeByte)
			{
				if (str != null)
					codegen.PutByte(str, out newaddr, !isPass1);
				else
					codegen.PutByte(val, 1, out newaddr, !isPass1);
			}
			else if (isTypeHalf)
			{
				codegen.PutHalfWord(val, 1, out newaddr, !isPass1);
			}
			else
			{
				if (isCodeSection)
					codegen.PutCodeWord(val, !isPass1);
				else
					codegen.PutWord(val, 1, out newaddr, !isPass1);
			}
			
			// Fix previous labels
			if (isPass1)
			{
				if (isIDataSection)
				{
					foreach (Symbol s in labelList_ID)
					{
						s.val = newaddr;
					}
					labelList_ID.Clear();
				}
				else if (isUDataSection)
				{
					foreach (Symbol s in labelList_UD)
					{
						s.val = newaddr;
					}
					labelList_UD.Clear();
				}
			}
			
			// Fetch more initializers
			if (hasInitializer)
			{
				while (sym == Token.Comma)
				{
					scanner.Get(out sym);
					if (isTypeByte && sym == Token.String)
					{
						codegen.PutByte(scanner.Name, out newaddr, !isPass1);
						scanner.Get(out sym);
					}
					else
					{
						val = GetExpression(out refsym);
						if (refsym != null)
							scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
						if (isTypeByte)
						{
							codegen.PutByte(val, 1, out newaddr, !isPass1);
						}
						else if (isTypeHalf)
						{
							codegen.PutHalfWord(val, 1, out newaddr, !isPass1);
						}
						else
						{
							if (isCodeSection)
								codegen.PutCodeWord(val, !isPass1);
							else
								codegen.PutWord(val, 1, out newaddr, !isPass1);
						}
					}
				}
			}
		}
		
		void GetExport()
		{
			scanner.Get(out sym);
			if (sym != Token.Ident)
			{
				scanner.Mark(ErrorType.IdentExpected);
				return;
			}
			if (passnum == 1)
				symtab.InsertExport(scanner.Name);
			scanner.Get(out sym);
			while (sym == Token.Comma)
			{
				scanner.Get(out sym);
				if (sym != Token.Ident)
				{
					scanner.Mark(ErrorType.IdentExpected);
					return;
				}
				if (passnum == 1)
					symtab.InsertExport(scanner.Name);
				scanner.Get(out sym);
			}
		}
		
		void GetImport()
		{
			scanner.Get(out sym);
			if (sym != Token.Ident)
			{
				scanner.Mark(ErrorType.IdentExpected);
				return;
			}
			if (passnum == 1)
				symtab.InsertImport(scanner.Name);
			scanner.Get(out sym);
			while (sym == Token.Comma)
			{
				scanner.Get(out sym);
				if (sym != Token.Ident)
				{
					scanner.Mark(ErrorType.IdentExpected);
					return;
				}
				if (passnum == 1)
					symtab.InsertImport(scanner.Name);
				scanner.Get(out sym);
			}
		}
		
		void GetDirective()
		{
			string name;
			int val;
			Symbol refsym;
			
			scanner.Get(out sym);
			if (sym != Token.Ident)
				scanner.Mark(ErrorType.DirectiveNameExpected);
			else
			{
				name = scanner.Name;
				switch (name)
				{
					case "align":
						if (codegen.CurrentSection == SectionType.Null)
							scanner.Mark(ErrorType.OutsideSection);
						scanner.Get(out sym);
						val = GetExpression(out refsym);
						codegen.Align((uint)val);
						if (refsym != null && codegen.OutputIsRelocatable)
							scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
						break;
					case "code":
					case "text":
						codegen.SetCurrentSection(SectionType.Code);
						break;
					case "data":
						codegen.SetCurrentSection(SectionType.InitializedData);
						break;
					case "bss":
						codegen.SetCurrentSection(SectionType.UninitializedData);
						break;
					case "export":
						if (!codegen.OutputIsRelocatable)
							scanner.Mark(ErrorType.RequiresRelocatableFormat);
						else
							GetExport();
						break;
					case "import":
						if (!codegen.OutputIsRelocatable)
							scanner.Mark(ErrorType.RequiresRelocatableFormat);
						else
							GetImport();
						break;
					case "resb":
					case "resh":
					case "resw":
						scanner.Get(out sym);
						GetDataReservation(name);
						break;
					case "byte":
					case "hword":
					case "word":
						scanner.Get(out sym);
						GetDataInitializer(name);
						break;
					case "define":
						scanner.Get(out sym);
						if (sym != Token.Ident)
							scanner.Mark(ErrorType.IdentExpected);
						else
						{
							name = scanner.Name;
							scanner.Get(out sym);
							val = GetExpression(out refsym);
							if (refsym != null && codegen.OutputIsRelocatable)
								scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
							if (passnum == 1)
							{
								symtab.InsertConstant(name, val);
							}
						}
						break;
					default:
						scanner.Mark(ErrorType.UnknownDirective);
						break;
				}
			}
			scanner.Get(out sym);
		}

		void GetInstructionTypeR(Mnemonic mne)
		{
			Register d, s1, s2;
			int imm;
			Symbol refsym;
			
			scanner.Get(out sym);
			if (sym != Token.Register)
			{
				scanner.Mark(ErrorType.RegisterExpected);
				return;
			}
			d = scanner.Register;
			scanner.Get(out sym);
			if (sym != Token.Comma)
			{
				scanner.Mark(ErrorType.CommaExpected);
				return;
			}
			scanner.Get(out sym);
			if (sym != Token.Register)
			{
				scanner.Mark(ErrorType.RegisterExpected);
				return;
			}
			s1 = scanner.Register;
			scanner.Get(out sym);
			if (sym != Token.Comma)
			{
				scanner.Mark(ErrorType.CommaExpected);
				return;
			}
			scanner.Get(out sym);
			if (sym == Token.Register)
			{
				if (passnum == 1)
				{
					codegen.AdvancePC(mne);
					return;
				}
				s2 = scanner.Register;
				scanner.Get(out sym);
				codegen.PutF0(mne, d, s1, s2);
			}
			else
			{
				imm = GetExpression(out refsym);
				if (passnum == 1)
				{
					codegen.AdvancePC(mne, imm);
					return;
				}
				if (refsym != null && codegen.OutputIsRelocatable)
				{
					scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
					return;
				}
				codegen.PutF1(mne, d, s1, imm);
			}
		}

		void GetInstructionTypeMov(Mnemonic mne)
		{
			Register d, s;
			int imm;
			Symbol refsym;

			scanner.Get(out sym);
			if (sym != Token.Register)
			{
				scanner.Mark(ErrorType.RegisterExpected);
				return;
			}
			d = scanner.Register;
			scanner.Get(out sym);
			if (sym != Token.Comma)
			{
				scanner.Mark(ErrorType.CommaExpected);
				return;
			}
			scanner.Get(out sym);
			if (sym == Token.Register)
			{
				if (mne != Mnemonic.MOV)
				{
					scanner.Mark(ErrorType.RegisterExpected);
					return;
				}
				if (passnum == 1)
				{
					codegen.AdvancePC(mne);
					return;
				}
				s = scanner.Register;
				scanner.Get(out sym);
				codegen.PutF0(Mnemonic.MOV, d, Register.R0, s);
			}
			else
			{
				if (mne == Mnemonic.MOVA && !codegen.OutputIsRelocatable)
					mne = Mnemonic.MOV;
				imm = GetExpression(out refsym);
				if (passnum == 1)
				{
					codegen.AdvancePC(mne, imm);
					return;
				}
				if (mne == Mnemonic.MOVA)
				{
					codegen.PutMOVA(d, imm, refsym);
				}
				else
				{
					codegen.PutF1(mne, d, Register.R0, imm);
					if (refsym != null && codegen.OutputIsRelocatable)
						scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
				}
			}
		}

		void GetInstructionTypeMem(Mnemonic mne)
		{
			Register d, s;
			int off;
			Symbol refsym;
			
			scanner.Get(out sym);
			if (sym != Token.Register)
			{
				scanner.Mark(ErrorType.RegisterExpected);
				return;
			}
			d = scanner.Register;
			scanner.Get(out sym);
			if (sym != Token.Comma)
			{
				scanner.Mark(ErrorType.CommaExpected);
				return;
			}
			scanner.Get(out sym);
			if (sym != Token.Register)
			{
				scanner.Mark(ErrorType.RegisterExpected);
				return;
			}
			s = scanner.Register;
			scanner.Get(out sym);
			if (sym != Token.Comma)
			{
				scanner.Mark(ErrorType.CommaExpected);
				return;
			}
			scanner.Get(out sym);
			off = GetExpression(out refsym);
			if (passnum == 1)
			{
				codegen.AdvancePC(mne, off);
				return;
			}
			if (refsym != null && codegen.OutputIsRelocatable)
				scanner.Mark(ErrorType.RelocatableSymbolNotAllowed);
			codegen.PutF2(mne, d, s, off);
		}

		void GetInstructionTypeBranch(Mnemonic mne)
		{
			int imm;
			Symbol refsym;

			if (passnum == 1)
			{
				codegen.AdvancePC(mne);
				return;
			}
			scanner.Get(out sym);
			if (sym == Token.Register)
			{
				Register r = scanner.Register;
				codegen.PutF3(mne, r);
			}
			else
			{
				imm = GetExpression(out refsym);
				if (refsym != null)
				{
					imm -= (int)codegen.PC + 4;
					if (refsym.type == SymbolType.Import)
						scanner.Mark(ErrorType.ImportedSymbolNotAllowed);
				}
				codegen.PutF3off(mne, imm);
			}
		}

		void GetInstructionTypeCall(Mnemonic mne)
		{
			int imm;
			Symbol refsym;

			if (passnum == 1)
			{
				codegen.AdvancePC(mne);
				return;
			}
			scanner.Get(out sym);
			if (sym == Token.Register)
			{
				Register r = scanner.Register;
				codegen.PutF3(mne, r);
			}
			else
			{
				imm = GetExpression(out refsym);
				if (refsym != null)
				{
					imm -= (int)codegen.PC;
				}
				codegen.PutF3off(mne, imm, refsym);
			}
		}

		void GetInstructionTypeSpecial1(Mnemonic mne)
		{
			Register r;

			if (passnum == 1)
			{
				codegen.AdvancePC(mne);
				return;
			}

			scanner.Get(out sym);
			if (sym != Token.Register)
			{
				scanner.Mark(ErrorType.RegisterExpected);
				return;
			}
			r = scanner.Register;
			scanner.Get(out sym);
			switch (mne)
			{
				case Mnemonic.NOT:
					codegen.PutF1(Mnemonic.XOR, r, r, -1);
					break;
				case Mnemonic.CLR:
					codegen.PutF1(Mnemonic.MOV, r, Register.R0, 0);
					break;
				case Mnemonic.RDH:
					codegen.PutF0(Mnemonic.RDH, r, Register.R0, Register.R0);
					break;
				case Mnemonic.RDF:
					codegen.PutF0(Mnemonic.RDF, r, Register.R0, Register.R0);
					break;
				case Mnemonic.INC:
					codegen.PutF1(Mnemonic.ADD, r, r, 1);
					break;
				case Mnemonic.DEC:
					codegen.PutF1(Mnemonic.SUB, r, r, 1);
					break;
			}
		}

		void GetInstructionTypeSpecial0(Mnemonic mne)
		{
			if (passnum == 1)
			{
				codegen.AdvancePC(mne);
				return;
			}

			scanner.Get(out sym);
			switch (mne)
			{
				case Mnemonic.RET:
					codegen.PutF3(Mnemonic.BRA, Register.LNK);
					break;
				case Mnemonic.NOP:
					codegen.PutF0(Mnemonic.MOV, Register.R0, Register.R0, Register.R0);
					break;
			}
		}

		void GetInstruction()
		{
			Mnemonic mne;

			if (codegen.CurrentSection != SectionType.Code)
			{
				scanner.Mark(ErrorType.OutsideSection);
				return;
			}

			if (passnum == 2)
			{
				if (genlist)
				{
					scanner.SetMnemonicStart();
					scanner.PrintLine(listwriter);
				}
			}

			mne = scanner.Mnemonic;
			switch (CodeGen.GetInstType(mne))
			{
				case InstructionType.R:
					GetInstructionTypeR(mne);
					break;
				case InstructionType.Mov:
					GetInstructionTypeMov(mne);
					break;
				case InstructionType.Mem:
					GetInstructionTypeMem(mne);
					break;
				case InstructionType.Branch:
					GetInstructionTypeBranch(mne);
					break;
				case InstructionType.Call:
					GetInstructionTypeCall(mne);
					break;
				case InstructionType.Special1:
					GetInstructionTypeSpecial1(mne);
					break;
				case InstructionType.Special0:
					GetInstructionTypeSpecial0(mne);
					break;
				default:
					Debug.Assert(false);
					break;
			}
		}
		
		void ParseLine()
		{
			GetLabel();
			
			if (scanner.ErrorOnLine) return;
			
			if (sym == Token.Period)
				GetDirective();
			else if (sym == Token.Mnemonic)
				GetInstruction();

			if (passnum == 2)
			{
				if (!scanner.ErrorOnLine && sym != Token.Eol)
					scanner.Mark(ErrorType.EOLExpected);
			}
		}
		
		public void Parse()
		{
			while (!scanner.Eof)
			{
				scanner.NextLine();
				scanner.Get(out sym);
				ParseLine();
			}
		}
	}
}
