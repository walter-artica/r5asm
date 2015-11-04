/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 28/05/2012
 * Time: 09:29 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace r5asm
{
	namespace PE
	{
		static public class Constants
		{
			public const int SIZEOF_SHORT_NAME = 8;
			public const UInt16 FILE_MACHINE_NIOS2 = 0x71;
			public const UInt16 FILE_32BIT_MACHINE = 0x0100;
			public const UInt16 FILE_DEBUG_STRIPPED = 0x0200;
			
			// Image section characteristics
			public const UInt32 SCN_CNT_CODE				= 0x00000020U;
			public const UInt32 SCN_CNT_INITIALIZED_DATA	= 0x00000040U;
			public const UInt32 SCN_CNT_UNINITIALIZED_DATA	= 0x00000080U;
			public const UInt32 SCN_LNK_INFO				= 0x00000200U;
			public const UInt32 SCN_LNK_REMOVE				= 0x00000800U;
			public const UInt32 SCN_GPREL					= 0x00008000U;
			public const UInt32 SCN_ALIGN_1BYTES			= 0x00100000U;
			public const UInt32 SCN_ALIGN_2BYTES			= 0x00200000U;
			public const UInt32 SCN_ALIGN_4BYTES			= 0x00300000U;
			public const UInt32 SCN_ALIGN_8BYTES			= 0x00400000U;
			public const UInt32 SCN_ALIGN_16BYTES			= 0x00500000U;
			public const UInt32 SCN_ALIGN_32BYTES			= 0x00600000U;
			public const UInt32 SCN_ALIGN_64BYTES			= 0x00700000U;
			public const UInt32 SCN_ALIGN_128BYTES			= 0x00800000U;
			public const UInt32 SCN_ALIGN_256BYTES			= 0x00900000U;
			public const UInt32 SCN_ALIGN_512BYTES			= 0x00A00000U;
			public const UInt32 SCN_ALIGN_1024BYTES			= 0x00B00000U;
			public const UInt32 SCN_ALIGN_2048BYTES			= 0x00C00000U;
			public const UInt32 SCN_ALIGN_4096BYTES			= 0x00D00000U;
			public const UInt32 SCN_ALIGN_8192BYTES			= 0x00E00000U;
			public const UInt32 SCN_ALIGN_MASK				= 0x00F00000U;
			public const UInt32 SCN_LNK_NRELOC_OVFL			= 0x01000000U;
			public const UInt32 SCN_MEM_DISCARDABLE			= 0x02000000U;
			public const UInt32 SCN_MEM_NOT_CACHED			= 0x04000000U;
			public const UInt32 SCN_MEM_NOT_PAGED			= 0x08000000U;
			public const UInt32 SCN_MEM_SHARED				= 0x10000000U;
			public const UInt32 SCN_MEM_EXECUTE				= 0x20000000U;
			public const UInt32 SCN_MEM_READ				= 0x40000000U;
			public const UInt32 SCN_MEM_WRITE				= 0x80000000U;
		}
		
		// Header for PE image files
		public struct CoffHeader
		{
			public UInt16 Machine;
			public UInt16 NumberOfSections;
			public UInt32 TimeDateStamp;
			public UInt32 PointerToSymbolTable;
			public UInt32 NumberOfSymbols;
			public UInt16 SizeOfOptionalHeader;
			public UInt16 Characteristics;
		}
		
		public struct SectionHeader
		{
			public unsafe fixed byte Name[Constants.SIZEOF_SHORT_NAME];
			public UInt32 VirtualSize;
			public UInt32 VirtualAddress;
			public UInt32 SizeOfRawData;
			public UInt32 PointerToRawData;
			public UInt32 PointerToRelocations;
			public UInt32 PointerToLinenumbers;
			public UInt16 NumberOfRelocations;
			public UInt16 NumberOfLinenumbers;
			public UInt32 Characteristics;
		}
	}
}
