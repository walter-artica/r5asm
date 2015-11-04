/*
 * Created by SharpDevelop.
 * User: waz
 * Date: 29/05/2012
 * Time: 01:40 p.m.
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace r5asm
{
	static class Utils
	{
		/// <param name="x">Value to be rounded</param>
		/// <param name="n">Power of 2</param>
		/// <returns>x rounded up to the next multiple of n</returns>
		public static uint RoundUp(uint x, uint n)
		{
			return (x + n-1) & ~(n-1);
		}
	}
}
