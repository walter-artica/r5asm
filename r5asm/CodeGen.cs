/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 29/01/2012
 * Time: 06:02 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace r5asm
{
	public enum InstructionType { R, Mov, Mem, Branch, Call, Special1, Special0 }
	enum InstructionFormat { Unknown, F0, F1, F2, F3, F3off }
	public enum Condition
	{
		MI, PL, EQ, NE, CS, CC, VS, VC,
		LS, HI, LT, GE, LE, GT, RA
	}
	public enum OutputType { Raw, Text, Hex, Elf, MsCoff }
	public enum SectionType { Null, Code, InitializedData, UninitializedData, String }
	
	sealed class CodeGen
	{
		const int CBUFSIZE = 4096; // in words
		const int IDBUFSIZE = 4096; // in bytes
		const uint NOP = 0x0001883AU;
		const uint FILESYMBOLSIZE = 4 + 4 + 2 + 2;
		
		enum RelocationType { Absolute, Rel32, GPRel32, Jmp }
		
		struct Relocation
		{
			public uint off;
			public RelocationType type;
			public Symbol symbol;
			
			public Relocation(uint off, RelocationType type, Symbol symbol)
			{
				this.off = off;
				this.type = type;
				this.symbol = symbol;
			}
		}
		
		Scanner scanner;
		SymbolTable symtab;
		OutputType outtype;
		uint[] cbuf;
		byte[] idbuf;
		uint cc; // Code counter
		uint idc; // Initialized data counter
		uint udc; // Uninitialized data counter
		string outname;
		SectionType currentSection;
		bool genlist;
		bool useAT;
		bool useGPbased;
		bool autoalign;
		uint alignval;
		List<Relocation> relocationList;
		System.IO.StreamWriter listwriter;
		
		public CodeGen(Scanner scanner, SymbolTable symtab, ProgramOptions options, System.IO.StreamWriter listingWriter)
		{
			cbuf = new uint[CBUFSIZE];
			idbuf = new byte[IDBUFSIZE];
			this.scanner = scanner;
			this.symtab = symtab;
			this.outtype = options.outtype;
			this.useGPbased = options.useGPbased;
			this.genlist = options.enableListing;
			this.useAT = options.useAT;
			this.listwriter = listingWriter;
		}
		
		public OutputType OutputType { get { return outtype; } }
		public SectionType CurrentSection { get { return currentSection; } }
		public bool OutputIsRelocatable { get { return outtype == OutputType.Elf; } }
		
		public uint PC
		{
			get
			{
				switch (currentSection)
				{
					case SectionType.Code:
						return cc;
					case SectionType.InitializedData:
						return idc;
					case SectionType.UninitializedData:
						return udc;
					default:
						Console.WriteLine("- ERROR: Illegal state in code generator.");
						return 0;
				}
			}
			
			private set
			{
				switch (currentSection)
				{
					case SectionType.Code:
						cc = value;
						break;
					case SectionType.InitializedData:
						idc = value;
						break;
					case SectionType.UninitializedData:
						udc = value;
						break;
					default:
						Console.WriteLine("- ERROR: Illegal state in code generator.");
						break;
				}
			}
		}
		
		public void Init()
		{
			cc = 0;
			idc = 0;
			udc = 0;
			currentSection = SectionType.Null;
			autoalign = true;
			relocationList = new List<CodeGen.Relocation>();
		}
		
		public static InstructionType GetInstType(Mnemonic m)
		{
			int n = (int)m;

			if (n < 0x200)
				return InstructionType.R;
			else if (n < 0x300)
				return InstructionType.Mov;
			else if (n < 0x400)
				return InstructionType.Mem;
			else if (n < 0x500)
				return InstructionType.Branch;
			else if (n < 0x600)
				return InstructionType.Call;
			else if (n < 0x700)
				return InstructionType.Special1;
			else
				return InstructionType.Special0;
		}
		
		public static uint GetHighPart(uint w)
		{
			return (w >> 16) & 0x0FFFFU;
		}
		
		public static uint GetLowPart(uint w)
		{
			return w & 0x0FFFFU;
		}
		
		static bool IsPowerOf2(uint w)
		{
			int nofOnes;
			
			nofOnes = 0;
			while (w != 0)
			{
				if ((w & 1) != 0)
				{
					nofOnes++;
					if (nofOnes > 1) w = 0;	// i.e. quit now!
				}
				w >>= 1;
			}
			
			return nofOnes == 1;
		}
		
		public void PutByte(string s, out uint newaddr, bool write)
		{
			if (currentSection == SectionType.InitializedData)
			{
				newaddr = idc;
				if (write)
				{
					for (int i = 0; i < s.Length; i++)
					{
						idbuf[idc] = (byte)s[i];
						++idc;
					}
				}
				else
				{
					idc += (uint)s.Length;
				}
			}
			else
			{
				newaddr = udc;
			}
		}
		
		public void PutByte(int b, int cnt, out uint newaddr, bool write)
		{
			byte val;
			
			if (b < sbyte.MinValue || byte.MaxValue < b)
				scanner.Mark(ErrorType.ImmediateOutOfRange);
			unchecked { val = (byte)b; }
			switch (currentSection)
			{
				case SectionType.InitializedData:
					newaddr = idc;
					if (write)
					{
						while (cnt > 0)
						{
							idbuf[idc] = val;
							idc += 1;
							--cnt;
						}
					}
					else
					{
						idc += (uint)cnt;
					}
					break;
				case SectionType.UninitializedData:
					newaddr = udc;
					udc += (uint)cnt;
					break;
				default:
					Console.WriteLine("- ERROR: Illegal state in code generator.");
					newaddr = 0;
					break;
			}
		}
		
		public void PutHalfWord(int h, int cnt, out uint newaddr, bool write)
		{
			ushort val;
			
			newaddr = 0;
			if (h < short.MinValue || ushort.MaxValue < h)
				scanner.Mark(ErrorType.ImmediateOutOfRange);
			unchecked { val = (ushort)h; }
			switch (currentSection)
			{
				case SectionType.InitializedData:
					idc = Utils.RoundUp(idc, autoalign ? 2 : alignval);
					newaddr = idc;
					if (write)
					{
						while (cnt > 0)
						{
							idbuf[idc] = (byte)(val & 0xFFU);
							idbuf[idc+1] = (byte)((val >> 8) & 0xFFU);
							idc += 2;
							--cnt;
						}
					}
					else
					{
						idc += (uint)cnt * 2;
					}
					break;
				case SectionType.UninitializedData:
					udc = Utils.RoundUp(udc, autoalign ? 2 : alignval);
					newaddr = udc;
					udc += 2 * (uint)cnt;
					break;
				#if DEBUG
				default:
					Console.WriteLine("- ERROR: Illegal state in code generator.");
					break;
				#endif
			}
		}
		
		public void ReserveCodeWord(int cnt, bool listable)
		{
			#if DEBUG
			if (currentSection != SectionType.Code)
				Console.WriteLine("- ERROR: Illegal state in code generator.");
			#endif
			while (cnt > 0)
			{
				if (genlist)
				{
					if (listable) PrintInstructionWord(InstructionFormat.F0, NOP);
				}
				cbuf[cc/4] = NOP;	// safely put NOPs
				cc += 4;
				
				--cnt;
			}
		}
		
		public void PutCodeWord(bool listable)
		{
			if (genlist)
			{
				if (listable) PrintInstructionWord(InstructionFormat.F0, NOP);
			}
			PutCodeWord(NOP);
		}
		
		public void PutCodeWord(int w, bool listable)
		{
			if (genlist)
			{
				if (listable) PrintInstructionWord(InstructionFormat.Unknown, (uint)w);
			}
			PutCodeWord((uint)w);
		}
		
		private void PutCodeWord(uint w)
		{
			#if DEBUG
			if (currentSection != SectionType.Code)
				Console.WriteLine("- ERROR: Illegal state in code generator.");
			#endif
			cbuf[cc/4] = w;
			cc += 4;
		}
		
		public void PutWord(int w, int cnt, out uint newaddr, bool write)
		{
			uint val;
			
			unchecked { val = (uint)w; }
			switch (currentSection)
			{
				case SectionType.InitializedData:
					idc = Utils.RoundUp(idc, autoalign ? 4 : alignval);
					newaddr = idc;
					if (write)
					{
						while (cnt > 0)
						{
							idbuf[idc] = (byte)(val & 0xFFU);
							idbuf[idc+1] = (byte)((val >> 8) & 0xFFU);
							idbuf[idc+2] = (byte)((val >> 16) & 0xFFU);
							idbuf[idc+3] = (byte)((val >> 24) & 0xFFU);
							idc += 4;
							--cnt;
						}
					}
					else
					{
						idc += (uint)cnt * 4;
					}
					break;
				case SectionType.UninitializedData:
					udc = Utils.RoundUp(udc, autoalign ? 4 : alignval);
					newaddr = udc;
					udc += 4 * (uint)cnt;
					break;
				default:
					Console.WriteLine("- ERROR: Illegal state in code generator.");
					newaddr = 0;
					break;
			}
		}
		
		public void Align(uint val)
		{
			if (val == 0)
			{
				autoalign = true;
			}
			else if (!IsPowerOf2(val))
			{
				scanner.Mark(ErrorType.InvalidAlignment);
			}
			else
			{
				autoalign = false;
				alignval = val;
				PC = Utils.RoundUp(PC, val);
			}
		}
		
		public void SetCurrentSection(SectionType st)
		{
			currentSection = st;
		}
		
		public void AdvancePC(Mnemonic m)
		{
			PC += 4;
		}
		
		public void AdvancePC(Mnemonic m, int imm)
		{
			uint w = (uint)imm;
			
			if (m == Mnemonic.MOVA)
				PC += 8;
			else if (m == Mnemonic.MOVHI)
				PC += 4;
			else if (!useAT)
			{
				PC += 4;
			}
			else
			{
				if (GetInstType(m) == InstructionType.Mem)
				{
					if (0 <= imm && imm < (1 << 20))
						PC += 4;
					else if ((imm & 0x0FFFF) == 0)
						PC += 12;
					else
						PC += 16;
				}
				else if (-65536 <= imm && imm <= 65535)
				{
					PC += 4;
				}
				else
				{
					if ((imm & 0x0FFFF) == 0)
						PC += 8;
					else
						PC += 12;
					if (m == Mnemonic.MOV)
						PC -= 4;
				}
			}
		}
		
		public void PutF0(Mnemonic op, Register d, Register s1, Register s2)
		{
			uint w;
			bool u = op == Mnemonic.ADC || op == Mnemonic.SBC || op == Mnemonic.MULU || op == Mnemonic.RDH || op == Mnemonic.RDF;
			bool v = op == Mnemonic.RDF;

			w = (u ? (2U << 28) : 0)
				| (v ? (1U << 28) : 0)
				| ((uint)d << 24)
				| ((uint)s1 << 20)
				| (((uint)op & 0x0FU) << 16)
				| (uint)s2;
			if (genlist) PrintInstructionWord(InstructionFormat.F0, w);
			PutCodeWord(w);
		}
		
		public void PutF1(Mnemonic op, Register d, Register s, int imm)
		{
			uint w;
			uint n = (uint)imm;
			bool u = op == Mnemonic.ADC || op == Mnemonic.SBC || op == Mnemonic.MULU || op == Mnemonic.MOVHI;
			
			if (
				Mnemonic.LSL <= op && op <= Mnemonic.ROR && (imm < 0 || 31 < imm)
				|| op == Mnemonic.MOVHI && (imm < -32768 || imm > 65535)
			)
				scanner.Mark(ErrorType.ImmediateOutOfRange);

			if (-65536 <= imm && imm <= 65535)
			{
				bool v = imm < 0;
				w = 0x40000000U
					| (u ? (2U << 28) : 0)
					| (v ? (1U << 28) : 0)
					| ((uint)d << 24)
					| ((uint)s << 20)
					| (((uint)op & 0x0FU) << 16)
					| (n & 0x0FFFFU);
				if (genlist) PrintInstructionWord(InstructionFormat.F1, w);
				PutCodeWord(w);
			}
			else if (op == Mnemonic.MOV)
			{
				PutF1(Mnemonic.MOVHI, d, Register.R0, (imm >> 16) & 0x0FFFF);
				if ((imm & 0x0FFFF) != 0)
				{
					PutF1(Mnemonic.IOR, d, d, imm & 0x0FFFF);
				}
			}
			else if (useAT)
			{
				PutF1(Mnemonic.MOVHI, Register.AT, Register.R0, (imm >> 16) & 0x0FFFF);
				if ((imm & 0x0FFFF) != 0)
				{
					PutF1(Mnemonic.IOR, Register.AT, Register.AT, imm & 0x0FFFF);
				}
				PutF0(op, d, s, Register.AT);
			}
			else
			{
				scanner.Mark(ErrorType.ImmediateOutOfRange);
			}
		}

		public void PutF2(Mnemonic op, Register d, Register s, int off)
		{
			uint w;
			uint n = (uint) off;
			
			if (0 <= off && off < (1 << 20))
			{
				w = 0x80000000U
					| (((uint)op & 0x3U) << 28)
					| ((uint)d << 24)
					| ((uint)s << 20)
					| (n & 0x0FFFFFU);
				if (genlist) PrintInstructionWord(InstructionFormat.F2, w);
				PutCodeWord(w);
			}
			else if (useAT)
			{
				PutF1(Mnemonic.MOVHI, Register.AT, Register.R0, (off >> 16) & 0x0FFFF);
				if ((off & 0x0FFFF) != 0)
				{
					PutF1(Mnemonic.IOR, Register.AT, Register.AT, off & 0x0FFFF);
				}
				PutF0(Mnemonic.ADD, Register.AT, Register.AT, s);
				PutF2(op, d, Register.AT, 0);
			}
			else
			{
				scanner.Mark(ErrorType.ImmediateOutOfRange);
			}
		}

		public void PutF3(Mnemonic op, Register r)
		{
			uint w;
			bool v = op >= Mnemonic.BLMI;

			w = 0xC0000000U
				| (v ? (1U << 28) : 0)
				| (((uint)op & 0x0FU) << 24)
				| (uint)r;
			if (genlist) PrintInstructionWord(InstructionFormat.F3, w);
			PutCodeWord(w);
		}

		public void PutF3off(Mnemonic op, int imm, Symbol refsym = null)
		{
			uint w;
			bool v = op >= Mnemonic.BLMI;

			if ((imm & 0x3) != 0)
				scanner.Mark(ErrorType.UnalignedTargetInstruction);
			if (refsym != null)
				relocationList.Add(new Relocation(cc, RelocationType.Jmp, refsym));
			imm >>= 2;
			if (imm < -8388608 || 8388607 < imm)
				scanner.Mark(ErrorType.BranchOutOfRange);
			w = 0xE0000000U
				| (v ? (1U << 28) : 0)
				| (((uint)op & 0x0FU) << 24)
				| ((uint)imm & 0x0FFFFFFU);
			if (genlist) PrintInstructionWord(InstructionFormat.F3off, w);
			PutCodeWord(w);
		}
		
		public void PutMOVA(Register d, int off, Symbol refsym)
		{
			if (useGPbased)
				relocationList.Add(new Relocation(cc, RelocationType.GPRel32, refsym));
			else
				relocationList.Add(new Relocation(cc, RelocationType.Rel32, refsym));
			PutF1(Mnemonic.MOVHI, d, Register.R0, (off >> 16) & 0x0FFFF);
			PutF1(Mnemonic.IOR, d, d, off & 0x0FFFF);
		}

		/*
		 * DISCARDED: It complicates the first pass and relocations
		 * 
		public void PutMOVI(Register d, int imm)
		{
			if (0 <= imm && imm <= (int)(ushort.MaxValue))
			{
				PutI(Mnemonic.ORI, d, Register.R0, imm);
			}
			else if (short.MinValue <= imm && imm <= short.MaxValue)
			{
				PutI(Mnemonic.ADDI, d, Register.R0, imm);
			}
			else
			{
				PutI(Mnemonic.ORHI, d, Register.R0, (int)((uint)(imm >> 16) & 0xFFFFU));
				PutI(Mnemonic.ORI, d, Register.R0, (int)((uint)imm & 0xFFFFU));
			}
		}
		*/
		
		void PrintInstructionWord(InstructionFormat fmt, uint w)
		{
			string s, h, bin, hexa;
			
			s = Convert.ToString(w, 2);
			if (s.Length != 32) s = new string('0', 32-s.Length) + s;
			
			h = String.Format("{0:X8}", w);
			hexa = h.Substring(0, 4) + " " + h.Substring(4, 4);

			switch (fmt)
			{
				case InstructionFormat.F0:
					bin = s.Substring(0, 4) + " " + s.Substring(4, 4) + " "
						+ s.Substring(8, 4) + " " + s.Substring(12, 4) + " "
						+ s.Substring(16, 12) + " " + s.Substring(28, 4);
					break;
				case InstructionFormat.F1:
					bin = s.Substring(0, 4) + " " + s.Substring(4, 4) + " "
						+ s.Substring(8, 4) + " " + s.Substring(12, 4) + " "
						+ s.Substring(16, 4) + "_" + s.Substring(20, 4) + "_"
						+ s.Substring(24, 4) + "_" + s.Substring(28, 4);
					break;
				case InstructionFormat.F2:
					bin = s.Substring(0, 4) + " " + s.Substring(4, 4) + " "
						+ s.Substring(8, 4) + " " + s.Substring(12, 4) + "_"
						+ s.Substring(16, 4) + "_" + s.Substring(20, 4) + "_"
						+ s.Substring(24, 4) + "_" + s.Substring(28, 4);
					break;
				case InstructionFormat.F3:
					bin = s.Substring(0, 4) + " " + s.Substring(4, 4) + " "
						+ s.Substring(8, 16) + " "
						+ s.Substring(24, 4) + " " + s.Substring(28, 4);
					break;
				case InstructionFormat.F3off:
					bin = s.Substring(0, 4) + " " + s.Substring(4, 4) + " "
						+ s.Substring(8, 4) + "_" + s.Substring(12, 4) + "_"
						+ s.Substring(16, 4) + "_" + s.Substring(20, 4) + "_"
						+ s.Substring(24, 4) + "_" + s.Substring(28, 4);
					break;
				default:
					bin = s.Substring(0, 4) + "_" + s.Substring(4, 4) + "_"
						+ s.Substring(8, 4) + "_" + s.Substring(12, 4) + "_"
						+ s.Substring(16, 4) + "_" + s.Substring(20, 4) + "_"
						+ s.Substring(24, 4) + "_" + s.Substring(28, 4);
					break;
			}
			listwriter.WriteLine("PC = {0:X4}: {1} | {2}", cc, hexa, bin);
		}
		
		uint SwapEndianness(uint x)
		{
			return ((x & 0x000000ff) << 24) +  // First byte
           			((x & 0x0000ff00) << 8) +   // Second byte
           			((x & 0x00ff0000) >> 8) +   // Third byte
           			((x & 0xff000000) >> 24);   // Fourth byte
		}

		string GetChecksum(string line)
		{
			byte b, sum;
			byte chksum;
			
			sum = 0;
			for (int i = 1; i < line.Length; i += 2)
			{
				byte.TryParse(line.Substring(i, 2),
				              System.Globalization.NumberStyles.HexNumber,
				              System.Globalization.NumberFormatInfo.InvariantInfo,
				              out b);
				unchecked { sum += b; }
			}
			unchecked {	chksum = (byte)((sum^0xFFU)+1); }
			
			return String.Format("{0:X2}", chksum);
		}
		
		public void Rebase()
		{
			if (outtype == OutputType.Raw || outtype == OutputType.Hex)
			{
				if (useGPbased)
				{
					symtab.RebaseSection(SectionType.UninitializedData, Utils.RoundUp(idc, 4));
				}
				else
				{
					symtab.RebaseSection(SectionType.InitializedData, cc);
					symtab.RebaseSection(SectionType.UninitializedData, Utils.RoundUp(cc + idc, 4));
				}
			}
		}
		
		public void WriteOutputFile(string outname)
		{
			this.outname = outname;
			switch (outtype)
			{
				case OutputType.Raw: WriteRawFile(); break;
				case OutputType.Text: WriteTextFile(); break;
				case OutputType.Hex: WriteHexFile(); break;
				case OutputType.Elf: WriteElfFile(); break;
				case OutputType.MsCoff: WriteMsCoffFile(); break;
			}
		}
				
		public void WriteRawFile()
		{
			System.IO.FileStream fs;
			System.IO.BinaryWriter w;
			
			if (outname == null) outname = scanner.ModName + ".bin";
			fs = new System.IO.FileStream(outname, System.IO.FileMode.Create);
			w = new System.IO.BinaryWriter(fs);
			for (int i = 0; i < cc; i += 4)
			{
				//w.Write(SwapEndianness(buf[i]));
				w.Write(cbuf[i/4]);
			}
			w.Seek((int)cc, System.IO.SeekOrigin.Begin);
			w.Write(idbuf, 0, (int)idc);
			w.Close();
		}
		
		public void WriteHexFile()
		{
			System.IO.StreamWriter w;
			string line;
			
			if (outname == null) outname = scanner.ModName + ".hex";
			w = new System.IO.StreamWriter(outname, false, System.Text.Encoding.ASCII);
			for (int i = 0; i < cc; i += 4)
			{
				line = ":04" + String.Format("{0:X4}", i) + "00" + String.Format("{0:X8}", cbuf[i/4]);
				line = line + GetChecksum(line);
				w.WriteLine(line);
			}
			w.WriteLine(":00000001FF");
			w.Close();
		}
		
		public void WriteTextFile()
		{
			System.IO.StreamWriter w;
			
			if (outname == null) outname = scanner.ModName + ".txt";
			w = new System.IO.StreamWriter(outname, false, System.Text.Encoding.ASCII);
			for (int i = 0; i < cc; i += 4)
			{
				w.WriteLine("{0:X8}", cbuf[i/4]);
			}
			w.Close();
		}
		
		public void WriteElfFile()
		{
			
		}
		
		public void WriteMsCoffFile()
		{
			PE.CoffHeader coffhdr;
			System.IO.FileStream fs;
			System.IO.BinaryWriter w;
			ushort nofsections;
			ushort nofrels;
			uint nofsyms;
			uint startOfRawData, rawDataPointer;
			uint startOfCode = 0, startOfIData = 0, startOfRels = 0, startOfSyms = 0, startOfStrs = 0;
			uint codeSize, idataSize, udataSize, strSize, symSize;
			bool codePresent, idataPresent, udataPresent, relsPresent, symsPresent;
			string strData;
			
			if (outname == null)
				outname = scanner.ModName + ".Obj";
			
			nofrels = (ushort) relocationList.Count;
			nofsyms = symtab.BuildFileSymbolTable(out strData);
			
			codeSize = cc;
			idataSize = idc;
			udataSize = udc;
			strSize = (uint)strData.Length;
			symSize = nofsyms * FILESYMBOLSIZE;
			codePresent = codeSize > 0;
			idataPresent = idataSize > 0;
			udataPresent = udataSize > 0;
			relsPresent = nofrels > 0;
			symsPresent = nofsyms > 0;
			nofsections = (ushort)((codePresent ? 1 : 0) + (idataPresent ? 1 : 0) + (udataPresent ? 1 : 0) + (symsPresent ? 1 : 0));
			
			// Calculate file offsets ("raw" pointers)
			startOfRawData =
				Utils.RoundUp(
					(uint)Marshal.SizeOf(typeof(PE.CoffHeader))
						+ nofsections*(uint)Marshal.SizeOf(typeof(PE.SectionHeader)),
					4
				);
			rawDataPointer = startOfRawData;
			if (codePresent)
			{
				startOfCode = rawDataPointer;
				rawDataPointer = rawDataPointer + codeSize;
			}
			if (idataPresent)
			{
				startOfIData = Utils.RoundUp(rawDataPointer, 4);
				rawDataPointer = startOfIData + idataSize;
			}
			if (symsPresent)
			{
				startOfStrs = Utils.RoundUp(rawDataPointer, 4);
				rawDataPointer = startOfStrs + strSize;
				startOfSyms = Utils.RoundUp(rawDataPointer, 4);
				rawDataPointer = startOfSyms + symSize;
			}
			startOfRels = relsPresent ? Utils.RoundUp(rawDataPointer, 4) : 0;
			
			// Write COFF header
			fs = new System.IO.FileStream(outname, System.IO.FileMode.Create);
			w = new System.IO.BinaryWriter(fs, System.Text.Encoding.ASCII);
			
			coffhdr = new PE.CoffHeader();
			coffhdr.Machine = PE.Constants.FILE_MACHINE_NIOS2;
			coffhdr.NumberOfSections = nofsections;
			coffhdr.Characteristics = PE.Constants.FILE_32BIT_MACHINE;
			coffhdr.NumberOfSymbols = nofsyms;
			coffhdr.PointerToSymbolTable = startOfSyms;
			coffhdr.TimeDateStamp = (uint)
				(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
				.TotalSeconds;
			w.Write(ToByteArray(coffhdr));
			
			// Write section headers (must be contiguous)
			if (codePresent)
			{
				WriteSectionHeader
				(
					".text",
					codeSize, startOfCode,
					nofrels, startOfRels,
					PE.Constants.SCN_CNT_CODE | PE.Constants.SCN_MEM_EXECUTE
					| PE.Constants.SCN_MEM_READ,
					w
				);
			}	
			if (idataPresent)
			{
				WriteSectionHeader
				(
					".data",
					idataSize, startOfIData,
					0, 0,
					PE.Constants.SCN_CNT_INITIALIZED_DATA
					| PE.Constants.SCN_MEM_READ
					| PE.Constants.SCN_MEM_WRITE,
					w
				);
			}
			if (udataPresent)
			{
				WriteSectionHeader
				(
					".udata",
					udataSize, 0,
					0, 0,
					PE.Constants.SCN_CNT_UNINITIALIZED_DATA
					| PE.Constants.SCN_MEM_READ
					| PE.Constants.SCN_MEM_WRITE,
					w
				);
			}
			if (symsPresent)
			{
				WriteSectionHeader
				(
					".string",
					strSize, startOfStrs,
					0, 0,
					PE.Constants.SCN_CNT_INITIALIZED_DATA
					| PE.Constants.SCN_MEM_READ
					| PE.Constants.SCN_MEM_DISCARDABLE,
					w
				);
			}
			w.Flush();
			// Write raw data
			if (codePresent)
			{
				w.Seek((int)startOfCode, System.IO.SeekOrigin.Begin);
				for (int i = 0; i < codeSize; i += 4)
				{
					w.Write(cbuf[i/4]);
				}
			}
			w.Flush();
			if (idataPresent)
			{
				w.Seek((int)startOfIData, System.IO.SeekOrigin.Begin);
				w.Write(idbuf, 0, (int)idataSize);
			}
			w.Flush();
			if (symsPresent)
			{
				List<Symbol> tab = symtab.FileTable;
				
				w.Seek((int)startOfStrs, System.IO.SeekOrigin.Begin);
				w.Write(strData.ToCharArray());
				w.Seek((int)startOfSyms, System.IO.SeekOrigin.Begin);
				for (int i = 0; i < tab.Count; i++)
				{
					Symbol s = tab[i];
					w.Write(s.strindex);
					w.Write(s.val);
					w.Write((ushort) (s.type == SymbolType.Import ? 0 : 1));
					w.Write((ushort)s.sectType);
				}
				w.Write(strData);
			}
			w.Flush();
			if (relsPresent)
			{
				WriteRelocations(startOfRels, w);
			}
			w.Flush();
			w.Close();
		}
		
		private void WriteSectionHeader
		(
			string secname,
			uint size,
			uint rawdatapointer,
			ushort nofrels,
			uint relsrawpointer,
			uint characteristics,
			System.IO.BinaryWriter w
		)
		{
			PE.SectionHeader hdr = new PE.SectionHeader();
			int i;
			
			for (i = 0; i < secname.Length; i++)
			{
				unsafe { hdr.Name[i] = (byte)secname[i]; }
			}
			// Not needed because constructor fills with zeroes
			/*
			int paddingcnt = PE.Constants.SIZEOF_SHORT_NAME-secname.Length;
			for (i = 0; i < paddingcnt; i++)
			{
				unsafe { hdr.Name[i] = 0; }
			}
			*/
			hdr.SizeOfRawData = size;
			hdr.PointerToRawData = rawdatapointer;
			hdr.Characteristics = characteristics;
			hdr.NumberOfRelocations = (ushort)nofrels;
			hdr.PointerToRelocations = relsrawpointer;
			w.Write(ToByteArray(hdr));
		}
		
		private void WriteRelocations(uint startOfRels, System.IO.BinaryWriter w)
		{
			Relocation rel;
			uint index;
			
			w.Seek((int)startOfRels, System.IO.SeekOrigin.Begin);
			for (int i = 0; i < relocationList.Count; i++)
			{
				rel = relocationList[i];
				w.Write(rel.off);
				index = rel.symbol.index;
				w.Write(index);
				w.Write((uint)rel.type);
			}
		}
		
		private byte[] ToByteArray<T>(T st) where T: struct
		{
			//Set the buffer to the correct size
			byte[] buffer = new byte[Marshal.SizeOf(st)];
			
			//Allocate the buffer to memory and pin it so that GC cannot use the
			//space (Disable GC)
			GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			
			// copy the struct into int byte[] mem alloc
			Marshal.StructureToPtr(st, h.AddrOfPinnedObject(), false);
        
			h.Free(); //Allow GC to do its job
			
			return buffer;
		}
	}
}
