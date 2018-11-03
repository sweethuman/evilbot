﻿using System;
using System.Collections.Generic;

namespace EvilBot.Utilities
{
	public static class CommandHelpers
	{
		//returns, first string is minutes, second is
		public static List<string> ManageCommandSorter(string stringOne, string stringTwo)
		{
			if (stringOne.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
			{
				var temporary = stringTwo;
				stringTwo = stringOne;
				stringOne = temporary;
			}

			var stringOrder = new List<string>();
			stringOrder.Add(stringOne);
			stringOrder.Add(stringTwo);
			return stringOrder;
		}
	}
}