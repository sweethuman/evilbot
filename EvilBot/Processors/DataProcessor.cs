﻿using EvilBot.DataStructures;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TwitchLib.Api.V5.Models.Subscriptions;
using TwitchLib.Api.V5.Models.Users;

namespace EvilBot.Processors
{
    internal class DataProcessor : IDataProcessor
    {
        private readonly IDataAccess _dataAccess;
        private readonly ITwitchConnections _twitchChatBot;
        private readonly List<Tuple<string, int>> ranks = new List<Tuple<string, int>>();

        public event EventHandler<RankUpdateEventArgs> RankUpdated;

        public static int RankNumber { get; private set; } = 8;

        protected virtual void OnRankUpdated(string Name, string Rank)
        {
            RankUpdated?.Invoke(this, new RankUpdateEventArgs { Name = Name, Rank = Rank });
        }

        public DataProcessor(IDataAccess dataAccess, ITwitchConnections twitchChatBot)
        {
            _dataAccess = dataAccess;
            _twitchChatBot = twitchChatBot;
            IntializeRanks();
        }

        private void IntializeRanks()
        {
            ranks.Add(new Tuple<string, int>("Fara Rank", 0));
            ranks.Add(new Tuple<string, int>("Rookie", 50));
            ranks.Add(new Tuple<string, int>("Alpha", 500));
            ranks.Add(new Tuple<string, int>("Thug", 2500));
            ranks.Add(new Tuple<string, int>("Sage", 6000));
            ranks.Add(new Tuple<string, int>("Lord", 10000));
            ranks.Add(new Tuple<string, int>("Initiate", 15000));
            ranks.Add(new Tuple<string, int>("Veteran", 22000));
            ranks.Add(new Tuple<string, int>("Emperor", 30000));

            RankNumber = ranks.Count;
        }

        public string GetRankFormatted(string rankString, string pointsString)
        {
            if (int.TryParse(rankString, out int place) && int.TryParse(pointsString, out int points))
            {
                if (place == 0)
                {
                    return $"{ranks[place].Item1} XP: {points}/{ranks[place + 1].Item2}";
                }
                if (place == ranks.Count - 1)
                {
                    return $"{ranks[place].Item1} (Lvl.{place}) XP: {points}";
                }
                return $"{ranks[place].Item1} (Lvl.{place}) XP: {points}/{ranks[place + 1].Item2}";
            }
            else
            {
                Log.Error("{rankString} {pointsString} is not a parsable value to int {method}", rankString, pointsString, $"{ToString()} GetRankFormatted");
                return null;
            }
        }

        #region DataProcessor TimedPointManagers

        private int GetRank(int points)
        {
            var place = 0;
            for (int i = 0; i < ranks.Count - 1; i++)
            {
                if (points < ranks[i + 1].Item2)
                {
                    break;
                }
                place = i + 1;
            }
            return place;
        }

        public async void AddLurkerPointsTimer_ElapsedAsync(object sender, ElapsedEventArgs e)
        {
            //in case twitch says something went wrong, it throws exception, catch that exception
            var userList = new List<IUserBase>();
            var chatusers = await _twitchChatBot.Api.Undocumented.GetChattersAsync(TwitchInfo.ChannelName).ConfigureAwait(false);
            var userIdTasks = new List<Task<string>>();
            for (int i = 0; i < chatusers.Count; i++)
            {
                userIdTasks.Add(GetUserIdAsync(chatusers[i].Username));
            }
            var userIDList = (await Task.WhenAll(userIdTasks).ConfigureAwait(false)).ToList();
            for (int i = 0; i < chatusers.Count; i++)
            {
                userList.Add(new UserBase(chatusers[i].Username, userIDList[i]));
            }
            await AddToUserAsync(userList, minutes: 10).ConfigureAwait(false);
            Log.Debug("Database updated! Lurkers present: {Lurkers}", chatusers.Count);
        }

        public async void AddPointsTimer_ElapsedAsync(object sender, ElapsedEventArgs e)
        {
            var temporaryTalkers = PointCounter.ClearTalkerPoints();
            await AddToUserAsync(temporaryTalkers).ConfigureAwait(false);
            Log.Debug("Database updated! Talkers present: {Talkers}", temporaryTalkers.Count);
        }

        /// <summary>
        /// Adds Points to the Users asynchronously.
        /// </summary>
        /// <param name="userList">The users to add too the defined values.</param>
        /// <param name="points">The points to add.</param>
        /// <param name="minutes">The minutes to add.</param>
        /// <param name="subCheck">if set to <c>true</c> it will check if users are subscribers.</param>
        /// <returns></returns>
        public async Task AddToUserAsync(List<IUserBase> userList, int points = 1, int minutes = 0, bool subCheck = true)
        {
            if (userList.Count != 0)
            {
                var pointsMultiplier = float.Parse(ConfigurationManager.AppSettings.Get("pointsMultiplier"));
                //t: make sub checking more efficient
                List<Subscription> channelSubscribers;
                if (subCheck)
                {
                    var channelId = await GetUserIdAsync(TwitchInfo.ChannelName).ConfigureAwait(false);
                    channelSubscribers = (await _twitchChatBot.Api.V5.Channels.GetChannelSubscribersAsync(channelId).ConfigureAwait(false)).Subscriptions.ToList();
                }
                else
                {
                    channelSubscribers = new List<Subscription>();
                }
                int pointAdderValue;
                var addPointsTasks = new List<Task>();
                for (int i = 0; i < userList.Count; i++)
                {
                    pointAdderValue = points;
                    if (channelSubscribers.Any(x => x.User.Id == userList[i].UserId))
                    {
                        pointAdderValue = (int)(pointAdderValue * pointsMultiplier);
                    }
                    addPointsTasks.Add(_dataAccess.ModifierUserIDAsync(userList[i].UserId, points: pointAdderValue, minutes: minutes));
                }
                await Task.WhenAll(addPointsTasks).ConfigureAwait(false);
                await UpdateRankAsync(userList).ConfigureAwait(false);
            }
        }

        private async Task UpdateRankAsync(List<IUserBase> userList)
        {   //!WARNING GetUserAttributesAsync() also gets minutes, wich I don't currently need and it might cause performance issues if volume is large
            var userAttributesTasks = new List<Task<List<string>>>();
            var userNameRanks = new List<int>();
            var usersUpdated = new List<IUserBase>();
            var databaseRankUpdateTasks = new List<Task>();
            for (int i = 0; i < userList.Count; i++)
            {
                userAttributesTasks.Add(GetUserAttributesAsync(userList[i].UserId));
            }
            var userAttributes = (await Task.WhenAll(userAttributesTasks).ConfigureAwait(false)).ToList();
            for (int i = 0; i < userAttributes.Count; i++)
            {
                if (!int.TryParse(userAttributes[i][0], out int points))
                {
                    Log.Error("Tried to parse string to int: {string} in {ClassSource}", userAttributes[i][1], $"{ToString()}UpdateRankAsync");
                }
                if (!int.TryParse(userAttributes[i][2], out int rank))
                {
                    Log.Error("Tried to parse string to int: {string} in {ClassSource}", userAttributes[i][1], $"{ToString()}UpdateRankAsync");
                }
                var currentRank = GetRank(points);
                if (currentRank != rank)
                {
                    userNameRanks.Add(currentRank);
                    databaseRankUpdateTasks.Add(_dataAccess.ModifyUserIDRankAsync(userList[i].UserId, currentRank));
                    usersUpdated.Add(userList[i]);
                }
            }
            await Task.WhenAll(databaseRankUpdateTasks).ConfigureAwait(false);
            for (int i = 0; i < usersUpdated.Count; i++)
            {
                OnRankUpdated(usersUpdated[i].DisplayName, $"{ranks[userNameRanks[i]].Item1} (Lvl. {userNameRanks[i]})");
            }
        }

        #endregion DataProcessor TimedPointManagers

        #region DataProcessor GeneralProcessors

        public async Task<TimeSpan?> GetUptimeAsync()
        {
            var userId = await GetUserIdAsync(TwitchInfo.ChannelName).ConfigureAwait(false);
            if (userId == null)
            {
                return null;
            }
            return _twitchChatBot.Api.V5.Streams.GetUptimeAsync(userId).Result;
        }

        public async Task<string> GetUserIdAsync(string username)
        {
            Log.Debug("AskedForID for {Username}", username);
            User[] userList;
            try
            {
                userList = (await _twitchChatBot.Api.V5.Users.GetUserByNameAsync(username).ConfigureAwait(false)).Matches;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetUserIdAsync blew up with {username}", username);
                return null;
            }
            if (username == null || userList.Length == 0)
            {
                return null;
            }
            return userList[0].Id;
        }

        public async Task<string> GetUsernameAsync(string userID)
        {
            Log.Debug("AskedForUsername for {Username}", userID);
            var user = await _twitchChatBot.Api.V5.Users.GetUserByIDAsync(userID).ConfigureAwait(false);
            if (userID == null || user == null)
            {
                return null;
            }
            return user.DisplayName;
        }

        public async Task<List<string>> GetUserAttributesAsync(string userID)
        {
            if (userID == null)
            {
                return null;
            }

            var tasks = new List<Task<string>>
            {
                _dataAccess.RetrieveRowAsync(userID),
                _dataAccess.RetrieveRowAsync(userID, Enums.DatabaseRow.Minutes),
                _dataAccess.RetrieveRowAsync(userID, Enums.DatabaseRow.Rank)
            };
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            if (results == null || results[0] == null)
            {
                return null;
            }

            return results.ToList();
        }

        #endregion DataProcessor GeneralProcessors
    }
}