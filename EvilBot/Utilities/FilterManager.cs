using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvilBot.DataStructures;
using EvilBot.DataStructures.Interfaces;
using EvilBot.Processors.Interfaces;
using EvilBot.Utilities.Interfaces;
using TwitchLib.Api.V5.Models.Users;

namespace EvilBot.Utilities
{
    public class FilterManager : IFilterManager
    {
        //TODO add somewhere code that if FilteredUsers table does not exist to be created
        private static List<IUserBase> FilteredUsers { get; } = new List<IUserBase>();
        private readonly IDataAccess _dataAccess;
        private readonly IDataProcessor _dataProcessor;
        public FilterManager(IDataAccess dataAccess, IDataProcessor dataProcessor)
        {
            _dataAccess = dataAccess;
            _dataProcessor = dataProcessor;
        }
        
        public async void InitializeFilter()
        {
            var users = await _dataAccess.RetrieveAllUsersFromTable(Enums.DatabaseTables.FilteredUsers);
            if (users == null) return;
            users.RemoveAll(x => x == null);
            var userListTasks = new List<Task<User>>();
            for (var i = 0; i < users.Count; i++)
            {
                userListTasks.Add(_dataProcessor.GetUserAsyncById(users[i].UserID));
            }

            var userList = (await Task.WhenAll(userListTasks)).ToList();
            userList.RemoveAll(x => x == null);
            for (var i = 0; i < userList.Count; i++)
            {
                FilteredUsers.Add(new UserBase(userList[i].DisplayName, userList[i].Id));
            }
        }

        public async Task AddToFiler(IUserBase user)
        {
            if (FilteredUsers.All(x => x.UserId != user.UserId))
            {
                FilteredUsers.Add(user);
            }
            //TODO update add if to update username if username changed
            //TODO save changes to database
        }

        public async Task<bool> RemoveFromFilter(IUserBase user)
        {
            if (FilteredUsers.All(x => x.UserId != user.UserId)) return false;
            {
                FilteredUsers.RemoveAll(x => x.UserId == user.UserId);
                return true;
            }

            //TODO save changes to database
        }
        
        public string RetrieveFilteredUsers()
        {
            if (FilteredUsers.Count <= 0)
            {
                return "/me Nici un User filtrat!";
            }
            var builder = new StringBuilder();
            builder.Append("/me Useri filtrati:");
            for (var i = 0; i < FilteredUsers.Count; i++)
            {
                builder.Append($" {FilteredUsers[i].DisplayName},");
            }

            return builder.ToString();
        }

        public static bool CheckIfUserFiltered(IUserBase user)
        {
            return FilteredUsers.Any(x => x.UserId == user.UserId);
        }
    }
}