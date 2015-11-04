/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 29/01/2012
 * Time: 03:45 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;

namespace r5asm
{
	public enum ErrorType
	{
		UnknownError, IllegalLabel, UnexpectedEOL, EOLExpected,
		RegisterExpected, ControlRegisterExpected, IllegalNumber,
		DirectiveNameExpected, StringDelimiterExpected, CountMustBePositive,
		SecondOperandExpected, OffsetExpected, ColonExpected, CommaExpected,
		LeftParenExpected, RightParenExpected, LeftSquareBracketExpected,
		IllegalExpression, ImmediateOutOfRange, BranchOutOfRange,
		DuplicatedDefinition, UndefinedIdentifier, InvalidAlignment,
		UnknownDirective, UnknownIdentifier, IdentExpected,
		OutsideSection, InitializedDataNotSupported, OnlyWordsInCode,
		InvalidOperationBetweenRelocatableSymbols, ImportedSymbolNotAllowed,
		InvalidOperationForRelocatableSymbol, RelocatableSymbolNotAllowed,
		RequiresRelocatableFormat, UnalignedTargetInstruction
	}
	public enum Token
	{
		Unknown, Ident, String, Number, Register, ControlRegister,
		CustomRegister, Mnemonic,
		Plus, Minus, Times, Slash, Or, And, Not,
		Colon, Comma, Period, Semicolon,
		Lparen, Rparen, Lbrak, Rbrak, Eol
	}
	public enum Mnemonic
	{
		// *** R ***
		LSL = 0x101, ASR = 0x102, ROR = 0x103,
		AND = 0x104, ANN = 0x105, IOR = 0x106, XOR = 0x107,
		ADD = 0x108, SUB = 0x109, MUL = 0x10A, DIV = 0x10B,
		FAD = 0x10C, FSB = 0x10D, FML = 0x10E, FDV = 0x10F,
		ADC = 0x110, SBC = 0x111, MULU = 0x112, CMP = SUB,

		// *** Mov ***
		MOV = 0x200, MOVHI = 0x210, MOVA = 0x220,

		// *** Mem ***
		LDW = 0x300, LDB = 0x301, STW = 0x302, STB = 0x303,

		// *** Branch ***
		BMI = 0x400, BEQ = 0x401, BCS = 0x402, BVS = 0x403,
		BLS = 0x404, BLT = 0x405, BLE = 0x406, BRA = 0x407,
		BPL = 0x408, BNE = 0x409, BCC = 0x40A, BVC = 0x40B,
		BHI = 0x40C, BGE = 0x40D, BGT = 0x40E, BRN = 0x40F,
		BZE = BEQ, BNZ = BNE,

		BLMI = 0x500, BLEQ = 0x501, BLCS = 0x502, BLVS = 0x503,
		BLLS = 0x504, BLLT = 0x505, BLLE = 0x506, BLA  = 0x507,
		BLPL = 0x508, BLNE = 0x509, BLCC = 0x50A, BLVC = 0x50B,
		BLHI = 0x50C, BLGE = 0x50D, BLGT = 0x50E, BLN  = 0x50F,
		BLZE = BLEQ, BLNZ = BLNE, CALL = BLA,
		
		// *** Special - 1 operand ***
		NOT = 0x600, CLR = 0x601, RDH = 0x602, RDF = 0x603,
		INC = 0x604, DEC = 0x605,

		// *** Special - no operands ***
		RET = 0x700, NOP = 0x701
	}

	public enum Register
	{
		R0, R1, R2, R3, R4, R5, R6, R7,
		R8, R9, R10, R11, R12, R13, R14, R15,
		AT = R11, MT = R12, SB = R13, SP = R14, LNK = R15
	}

	sealed class Scanner
	{
		const int TABSIZE = 8;
		const int MAXERRORS = 32;

		private string modName;
		private string modFilename;
		public bool ErrorOnLine;
		public int ErrorCount;

		string[] lines;
		string line;
		int linenum, pos;
		int tokenstartpos;
		readonly Dictionary<string, Mnemonic> mnemonicDictionary;
		readonly Dictionary<string, Register> registerDictionary;
		string name;
		uint number;
		Mnemonic mnemonic;
		Register register;
		int mnemonicstart;

		public string Name { get { return name; } }
		public uint Number { get { return number; } }
		public Mnemonic Mnemonic { get { return mnemonic; } }
		public Register Register { get { return register; } }
		public bool Eof { get { return linenum >= lines.Length; } }
		public string ModName { get { return modName; } }
		public string ModFilename { get { return modFilename; } }

		public void Mark(ErrorType err)
		{
			string s;

			ErrorOnLine = true;
			++ErrorCount;
			if (ErrorCount <= MAXERRORS)
			{
				switch (err)
				{
					case ErrorType.IllegalLabel: s = "Illegal label"; break;
					case ErrorType.IllegalNumber: s = "Illegal characters in number"; break;
					case ErrorType.DirectiveNameExpected: s = "Directive name expected"; break;
					case ErrorType.StringDelimiterExpected: s = "String delimiter expected"; break;
					case ErrorType.CountMustBePositive: s = "Count must be positive"; break;
					case ErrorType.UnexpectedEOL: s = "Unexpected end of line"; break;
					case ErrorType.EOLExpected: s = "End of line expected"; break;
					case ErrorType.ColonExpected: s = "Colon expected"; break;
					case ErrorType.CommaExpected: s = "Comma expected"; break;
					case ErrorType.RegisterExpected: s = "Register expected"; break;
					case ErrorType.ControlRegisterExpected: s = "Control register expected"; break;
					case ErrorType.SecondOperandExpected: s = "Second operand expected"; break;
					case ErrorType.OffsetExpected: s = "Offset expected"; break;
					case ErrorType.LeftParenExpected: s = "Left parenthesis expected"; break;
					case ErrorType.RightParenExpected: s = "Right parenthesis expected"; break;
					case ErrorType.IllegalExpression: s = "Illegal expression"; break;
					case ErrorType.ImmediateOutOfRange: s = "Immediate is out of range"; break;
					case ErrorType.BranchOutOfRange: s = "Branch target is out of range"; break;
					case ErrorType.DuplicatedDefinition: s = "Duplicated definition"; break;
					case ErrorType.UndefinedIdentifier: s = "Undefined identifier"; break;
					case ErrorType.InvalidAlignment: s = "Alignment value must be a power of 2"; break;
					case ErrorType.UnknownDirective: s = "Unknown directive"; break;
					case ErrorType.UnknownIdentifier: s = "Unknown identifier"; break;
					case ErrorType.IdentExpected: s = "Identifier expected"; break;
					case ErrorType.OutsideSection: s = "Outside section"; break;
					case ErrorType.OnlyWordsInCode: s = "Only words can be defined in code section"; break;
					case ErrorType.InitializedDataNotSupported:
						s = "Initialized data is not supported in this type of section"; break;
					case ErrorType.InvalidOperationBetweenRelocatableSymbols:
						s = "Invalid operation between relocatable symbols"; break;
					case ErrorType.InvalidOperationForRelocatableSymbol:
						s = "Invalid operation for relocatable symbol"; break;
					case ErrorType.RelocatableSymbolNotAllowed:
						s = "Relocatable symbol not allowed in this expression"; break;
					case ErrorType.ImportedSymbolNotAllowed:
						s = "Imported symbol not allowed in this expression"; break;
					case ErrorType.RequiresRelocatableFormat:
						s = "This requires a relocatable output format"; break;
					case ErrorType.UnalignedTargetInstruction:
						s = "Unaligned target instruction"; break;
					default: s = "Unknown error"; break;
				}
				Console.WriteLine("{0}({1}) : error: {2}", modFilename, linenum, s);

				if (ErrorCount == MAXERRORS)
				{
					Console.WriteLine("- FATAL: Too many errors.");
					//System.Environment.Exit(1);
				}
			}
		}

		public void MarkExport(string name)
		{
			++ErrorCount;
			if (ErrorCount <= MAXERRORS)
			{
				Console.WriteLine("- Error: Missing definition for exported identifier '" + name + "'");
			}
		}

		public void Init(string fname)
		{
			modName = System.IO.Path.GetFileNameWithoutExtension(fname);
			modFilename = System.IO.Path.GetFileName(fname);
			try
			{
				lines = System.IO.File.ReadAllLines(fname);
			}
			catch
			{
				Console.WriteLine("- FATAL: Can't read file: {0}", fname);
				System.Environment.Exit(1);
			}
			linenum = 0; pos = tokenstartpos = 0;
			ErrorOnLine = false;
			ErrorCount = 0;
		}

		public void Reset()
		{
			linenum = 0;
		}

		public void NextLine()
		{
			if (linenum < lines.Length)
			{
				do
				{
					line = lines[linenum];
					++linenum;
				} while (line.Trim() == "");
				pos = tokenstartpos = 0;
				ErrorOnLine = false;
			}
		}

		bool IsAlpha(char ch)
		{
			ch = Char.ToUpper(ch);
			return 'A' <= ch && ch <= 'Z';
		}

		bool IsDigit(char ch)
		{
			return '0' <= ch && ch <= '9';
		}

		bool IsAlphaNum(char ch)
		{
			ch = Char.ToUpper(ch);
			return 'A' <= ch && ch <= 'Z' || '0' <= ch && ch <= '9';
		}

		bool IsBlank(char ch)
		{
			return ch <= ' ';
		}

		void SkipBlanks()
		{
			while (pos < line.Length && line[pos] <= ' ')
			{
				++pos;
			}
		}

		void GetIdent(out Token sym)
		{
			char ch;
			bool isValid;
			string key;

			name = ""; isValid = true;
			while (pos < line.Length && isValid)
			{
				ch = Char.ToUpper(line[pos]);
				if ('A' <= ch && ch <= 'Z' || '0' <= ch && ch <= '9' || ch == '$' || ch == '_')
				{
					name += line[pos];
					++pos;
				}
				else isValid = false;
			}

			key = name.ToUpper();
			if (mnemonicDictionary.ContainsKey(key))
			{
				mnemonic = mnemonicDictionary[key];
				sym = Token.Mnemonic;
			}
			else if (registerDictionary.ContainsKey(key))
			{
				register = registerDictionary[key];
				sym = Token.Register;
			}
			else
			{
				sym = Token.Ident;
			}
		}

		void GetNumber(out Token sym)
		{
			char ch; string strval;
			bool isValid;

			strval = "";
			if (line[pos] == '0')
			{
				strval = "0";
				pos++;
			}
			if (pos < line.Length && line[pos] == 'x')
			{
				strval = "";
				pos++;
				do
				{
					ch = char.ToUpper(line[pos]);
					if ('0' <= ch && ch <= '9' || 'A' <= ch && ch <= 'F')
					{
						isValid = true;
						strval += ch;
						++pos;
					}
					else isValid = false;
				} while (pos < line.Length && isValid);
				if (!isValid && IsAlpha(ch) && ch > 'F')
					Mark(ErrorType.IllegalNumber);
				isValid = UInt32.TryParse(strval,
										  System.Globalization.NumberStyles.HexNumber,
										  System.Globalization.NumberFormatInfo.CurrentInfo,
										  out number);
				if (!isValid) Mark(ErrorType.ImmediateOutOfRange);
			}
			else
			{
				isValid = true; ch = '0';
				while (pos < line.Length && isValid)
				{
					ch = line[pos];
					if ('0' <= ch && ch <= '9')
					{
						strval += ch;
						++pos;
					}
					else isValid = false;
				}
				if (!isValid && IsAlpha(ch))
					Mark(ErrorType.IllegalNumber);
				isValid = UInt32.TryParse(strval, out number);
				if (!isValid) Mark(ErrorType.ImmediateOutOfRange);
			}

			sym = Token.Number;
		}

		void GetString(out Token sym)
		{
			char delimiter;
			string s;

			delimiter = line[pos];
			pos++;
			s = "";
			while (pos < line.Length && line[pos] != delimiter)
			{
				s += line[pos];
				++pos;
			}

			if (pos >= line.Length)
				Mark(ErrorType.UnexpectedEOL);
			else if (line[pos] != delimiter)
				Mark(ErrorType.StringDelimiterExpected);
			else
				pos++;

			if (s.Length <= 1)
			{
				sym = Token.Number;
				number = (byte)s[0];
			}
			else
			{
				sym = Token.String;
				name = s;
			}
		}

		public void Get(out Token sym)
		{
			char ch;

			SkipBlanks();
			if (pos >= line.Length) sym = Token.Eol;
			else
			{
				tokenstartpos = pos;
				ch = line[pos];
				switch (ch)
				{
					case '$':
					case '_':
						GetIdent(out sym);
						break;
					case '"':
					case '\'':
						GetString(out sym);
						break;
					case '+':
						sym = Token.Plus; ++pos;
						break;
					case '-':
						sym = Token.Minus; ++pos;
						break;
					case '*':
						sym = Token.Times; ++pos;
						break;
					case '/':
						sym = Token.Slash; ++pos;
						break;
					case ':':
						sym = Token.Colon; ++pos;
						break;
					case ',':
						sym = Token.Comma; ++pos;
						break;
					case '.':
						sym = Token.Period; ++pos;
						break;
					case '(':
						sym = Token.Lparen; ++pos;
						break;
					case ')':
						sym = Token.Rparen; ++pos;
						break;
					case '[':
						sym = Token.Lbrak; ++pos;
						break;
					case ']':
						sym = Token.Rbrak; ++pos;
						break;
					case '|':
						sym = Token.Or; ++pos;
						break;
					case '&':
						sym = Token.And; ++pos;
						break;
					case '~':
						sym = Token.Not; ++pos;
						break;
					case ';':
						sym = Token.Eol;
						pos = line.Length;
						break;
					default:
						if (IsAlpha(ch)) GetIdent(out sym);
						else if (IsDigit(ch)) GetNumber(out sym);
						else { sym = Token.Unknown; ++pos; }
						break;
				}
			}
		}

		void InitDictionaryFromEnum<T>(out Dictionary<string, T> dict)
		{
			string[] names;

			names = Enum.GetNames(typeof(T));
			dict = new Dictionary<string, T>(names.Length);
			for (int i = 0; i < names.Length; ++i)
			{
				dict.Add(names[i], (T)Enum.Parse(typeof(T), names[i]));
			}
		}

		public void PrintLine(System.IO.StreamWriter w)
		{
			w.WriteLine(new String(' ', 11) + line.Substring(mnemonicstart));
		}

		public void SetMnemonicStart()
		{
			mnemonicstart = tokenstartpos;
		}

		public Scanner()
		{
			InitDictionaryFromEnum<Mnemonic>(out mnemonicDictionary);
			InitDictionaryFromEnum<Register>(out registerDictionary);
		}
	}
}