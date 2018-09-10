﻿using Dapper;
using Serilog;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;

namespace EvilBot
{
    public class SqliteDataAccess : IDataAccess
    {
        private IDbConnection RetrieveConnection { get; } = new SQLiteConnection(LoadConnectionString("read_only"));
        private IDbConnection WriteConnection { get; } = new SQLiteConnection(LoadConnectionString());

        //TODO change bool to enum and add an exception handler for when a row i'm asking for doesn't exist
        public async Task<string> RetrieveRowAsync(string userID, Enums.DatabaseRow databaseRow = Enums.DatabaseRow.Points)
        {
            if (userID == null)
            {
                return null;
            }
            string column = databaseRow.ToString();

            var output = await RetrieveConnection.QueryAsync<string>($"SELECT {column} FROM UserPoints WHERE UserID = '{userID}'", new DynamicParameters()).ConfigureAwait(false);
            if (!output.Any()) //NOTE this was changed as well, test this method too
            {
                return null;
            }
            return output.ToList()[0];
        }

        public async Task ModifierUserIDAsync(string userID, int points = 1, int minutes = 0)
        {
            if (userID == null)
            {
                return;
            }

            if (!(await WriteConnection.QueryAsync<string>($"SELECT UserID from UserPoints WHERE UserID = '{userID}'", new DynamicParameters()).ConfigureAwait(false)).Any())
            {
                Log.Debug("Added a new User to Database with {UserID}", userID);
                await WriteConnection.ExecuteAsync($"INSERT INTO UserPoints (UserID, Points, Minutes) VALUES ('{userID}', '{points}', '{minutes}')").ConfigureAwait(false);
            }
            else
            {
                Log.Debug("Updated a User with {UserID} with {Minutes}", userID, minutes);
                await WriteConnection.ExecuteAsync($"UPDATE UserPoints SET Points = Points + {points}, Minutes = Minutes + {minutes} WHERE UserID = '{userID}'").ConfigureAwait(false);
            }
        }

        public async Task ModifyUserIDRankAsync(string userID, int rank)
        {
            if (userID == null)
            {
                return;
            }

            Log.Debug("Advanced a User with {UserID} with [{Rank}]", userID, rank);
            await WriteConnection.ExecuteAsync($"UPDATE UserPoints SET Rank = {rank} WHERE UserID = '{userID}'").ConfigureAwait(false);
        }

        private static string LoadConnectionString(string id = "Default")
        {
            return ConfigurationManager.ConnectionStrings[id].ConnectionString;
        }
    }
}