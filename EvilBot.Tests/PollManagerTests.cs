using System.Collections.Generic;
using Autofac.Extras.Moq;
using EvilBot.Utilities;
using EvilBot.Utilities.Resources.Interfaces;
using Xunit;

namespace EvilBot.Tests
{
	public class PollManagerTests
	{

		public static IEnumerable<object[]> GetPollCreateParams => new[]
		{
			new object[]
			{
				new List<string> { "cat", "dog", "pig", "hamster" },
				new List<string> { "cat", "dog", "pig", "hamster" }
			},

			new object[]
			{
				new List<string> { "cat", null, "pig", "hamster" },
				null
			},

			new object[]
			{
				new List<string> { "cat", "", "pig", "hamster" },
				null
			},

			new object[] { null, null },
			new object[] { new List<string>(), null },
			new object[] { new List<string> { "cat" }, null},
			new object[] { new List<string> { "hamsette", "hamster" }, new List<string> { "hamsette", "hamster" }}
		};


		[Theory]
		[MemberData(nameof(GetPollCreateParams))]
		public void PollCreate_ReturnCorrectStringAndStoreDataCorrectly(List<string> input, List<string> expected)
		{
			using (var mock = AutoMock.GetLoose())
			{
				mock.Mock<IDataAccess>();
				var cls = mock.Create<PollManager>();
				var result = cls.PollCreate(input);
				Assert.Equal(expected,result);
			}
		}

	}
}