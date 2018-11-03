﻿using System.Collections.Generic;
using System.Linq;
using EvilBot.DataStructures.Interfaces;
using Serilog;

namespace EvilBot.Utilities
{
	internal static class PointCounter
	{
		public static List<IUserBase> Talkers { get; private set; } = new List<IUserBase>();

		public static void AddMessagePoint(IUserBase user)
		{
			Log.Debug("AddMessagePoint ran for {User}({userID})", user.DisplayName, user.UserId);
			if (Talkers.All(x => x.UserId != user.UserId)) Talkers.Add(user);
		}

		public static List<IUserBase> ClearTalkerPoints()
		{
			var output = Talkers;
			Talkers = new List<IUserBase>();
			return output;
		}
	}
}