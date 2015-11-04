using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace r5sim
{
	class Simulator
	{
		const int
			MOV = 0,
			LSL = 1,
			ASR = 2,
			ROR = 3,
			AND = 4,
			ANN = 5,
			IOR = 6,
			XOR = 7,
			ADD = 8,
			SUB = 9,
			MUL = 10,
			DIV = 11,
			FAD = 12,
			FSB = 13,
			FML = 14,
			FDV = 15;

		uint[] M;
		string[] args;
		int codelen;

		uint IR;	// instruction register
		int PC;		// program counter
		bool N, Z, Ca, V;	// condition flags
		int[] R = new int[16];
		int H;		// aux register for division

		public Simulator(string filename, string[] args, int memsize = 2048)
		{
			M = new uint[memsize];

			try
			{
				int j = 0;
				string ext = System.IO.Path.GetExtension(filename);
				if (ext == ".txt")
				{
					string[] lines = System.IO.File.ReadAllLines(filename);
					for (int i = 0; i < lines.Length; i++)
					{
						string s = lines[i].Trim();
						if (s != "")
							try
							{
								M[j++] = Convert.ToUInt32(lines[i], 16);
							}
							catch
							{
								throw new FormatException();
							}
					}
					if (j == 0)
						throw new ArgumentOutOfRangeException();
				}
				else if (ext == ".bin")
				{
					System.IO.BinaryReader r = new System.IO.BinaryReader(System.IO.File.Open(filename, System.IO.FileMode.Open));
					while (r.BaseStream.Position < r.BaseStream.Length)
						M[j++] = r.ReadUInt32();
				}
				else
				{
					throw new ArgumentException();
				}
				this.codelen = j;
				this.args = args;
			}
			catch (FormatException e)
			{
				Console.WriteLine("- FATAL: Bad source file contents.");
				e.Data[0] = -4;
				throw e;
			}
			catch (ArgumentOutOfRangeException e)
			{
				Console.WriteLine("- FATAL: Empty source file.");
				e.Data[0] = -3;
				throw e;
			}
			catch (ArgumentException e)
			{
				Console.WriteLine("- FATAL: Unknown file type.");
				e.Data[0] = -2;
				throw e;
			}
			catch (Exception e)
			{
				Console.WriteLine("- FATAL: Can't read source file.");
				e.Data[0] = -1;
				throw e;
			}
		}

		int FloatOp(uint op, int b, int c)
		{
			float fa;
			float fb = BitConverter.ToSingle(BitConverter.GetBytes(b), 0);
			float fc = BitConverter.ToSingle(BitConverter.GetBytes(c), 0);
			
			switch (op)
			{
				case FAD: fa = fb + fc; break;
				case FSB: fa = fb - fc; break;
				case FML: fa = fb * fc; break;
				case FDV: fa = fb / fc; break;
				default: throw new InvalidOperationException("float op");
			}
			return BitConverter.ToInt32(BitConverter.GetBytes(fa), 0);
		}

		void OnError(string msg, int retval)
		{
			Console.WriteLine();
			Console.WriteLine("Fatal error: {0}", msg);
			Console.WriteLine("PC    = {0:X8} ", (PC - 1) * 4);

			Console.Write("R[0] = {0:X8}   ", R[0]);
			Console.Write("R[1] = {0:X8}   ", R[1]);
			Console.Write("R[2] = {0:X8}   ", R[2]);
			Console.WriteLine("R[3] = {0:X8}", R[3]);

			Console.Write("MT   = {0:X8}   ", R[12]);
			Console.Write("SB   = {0:X8}   ", R[13]);
			Console.Write("SP   = {0:X8}   ", R[14]);
			Console.WriteLine("LNK  = {0:X8} ", R[15]);

			Exception e = new InvalidProgramException(msg);
			e.Data[0] = retval;
			throw e;
		}

		public void Execute()
		{
			uint a, b, op, im;	// instruction fields
			int adr, A, B, C;
			int inputPtr;
			bool p, q, u, v;

			A = 0;
			inputPtr = 0;
			PC = 0;
			R[13] = codelen * 4;
			R[14] = M.Length*4;

			do // interpretation cycle
			{
				IR = M[PC];
				PC++;
				a = (IR >> 24) & 0x0FU;
				b = (IR >> 20) & 0x0FU;
				op = (IR >> 16) & 0x0FU;
				im = IR & 0x0FFFF;
				p = (IR & 0x80000000U) != 0;
				q = (IR & 0x40000000U) != 0;
				u = (IR & 0x20000000U) != 0;
				v = (IR & 0x10000000U) != 0;

				if (!p) // ~p:  register instruction
				{
					B = R[b];
					if (!q)
						C = R[IR & 0x0FU];
					else if (!v)
						C = (int)im;
					else
						C = (int) (0xFFFF0000U | im);
					int shim = C & 0x1F;
					switch (op)
					{
						case MOV:
							if (!u)
							{
								A = C;
							}
							else if (!q)
							{
								if (!v)
									A = H;
								else
									A = (Convert.ToInt32(N) << 31)
										| (Convert.ToInt32(Z) << 30)
										| (Convert.ToInt32(Ca) << 29)
										| (Convert.ToInt32(V) << 28)
										| 0x50;
							}
							else
							{
								A = C << 16;
							}
							break;
						case LSL:
							A = B << shim;
							break;
						case ASR:
							A = B >> shim;
							break;
						case ROR:
							A = (B << (32-shim)) | ((B >> shim) & 0x7FFFFFFF);
							break;
						case AND:
							A = B & C;
							break;
						case ANN:
							A = B & (~C);
							break;
						case IOR:
							A = B | C;
							break;
						case XOR:
							A = B ^ C;
							break;
						case ADD:
							A = B + C;
							Ca = (uint)A < (uint)B;
							V = ((A ^ B) & (A ^ C)) < 0;
							break;
						case SUB:
							A = B - C;
							Ca = (uint)B < (uint)C;
							V = ((B ^ C) & (B ^ A)) < 0;
							break;
						case MUL:
							A = B * C;
							break;
						case DIV:
							A = B / C;
							H = B % C;
							break;
						default:
							A = FloatOp(op, B, C);
							break;
					} // switch
					R[a] = A;
					N = A < 0;
					Z = A == 0;
				}
				else if (!q) // p & ~q: memory instruction
				{
					adr = (R[b] + (int)(IR & 0x0FFFFF)) / 4;
					if (!u)	// load
					{
						if (adr >= 0)
						{
							if (adr >= M.Length)
								OnError("Invalid memory address", -5);
							R[a] = (int)M[adr];
						}
						else // input
						{
							if (adr == -1) // ReadInt
							{
								if (inputPtr >= args.Length)
									OnError("Not enough input", -6);
								R[a] = Convert.ToInt32(args[inputPtr]);
								inputPtr++;
							}
							else if (adr == -2) // EOF
							{
								Z = inputPtr >= args.Length;
							}
						}
					}
					else // store
					{
						if (adr >= 0)
						{
							M[adr] = (uint)R[a];
						}
						else // output
						{
							if (adr == -1)
								Console.Write("{0,4}", R[a]);
							else if (adr == -2)
								Console.Write((char)(R[a] & 0x0FF));
							else if (adr == -3)
								Console.WriteLine();
						}
					}
				}
				else // p & q: branch instruction
				{
					if (
						   (a == 0) && N
						|| (a == 1) && Z
						|| (a == 2) && Ca
						|| (a == 3) && V
						|| (a == 4) && (!Ca || Z)
						|| (a == 5) && (N != V)
						|| (a == 6) && ((N != V) || Z)
						|| (a == 7)
						|| (a == 8) && !N
						|| (a == 9) && !Z
						|| (a == 10) && !Ca
						|| (a == 11) && !V
						|| (a == 12) && !(!Ca || Z)
						|| (a == 13) && (N == V)
						|| (a == 14) && !((N != V) || Z)
					)
					{
						if (v)
							R[15] = PC * 4;
						if (u)
							PC = (PC + (int)(IR & 0x0FFFFFFU)) & 0x3FFFF;
						else
							PC = R[IR & 0x0F] / 4;
					}
				}
			} while (PC != 0);
		}
	}
}
