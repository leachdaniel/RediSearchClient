using StackExchange.Redis;
using System;
using System.Linq;
using Xunit;

namespace RediSearchClient.Tests
{
    public class SearchResultTests
    {
        [Fact]
        public void CanDynamicallyCreateResultMapping()
        {
            var searchResult = SearchResult.From(FakeSearchResult);

            var mappedResult = searchResult.As<Movie>();

            var firstResult = mappedResult.First();

            Assert.Equal("Some movie that doesn't exist", firstResult.Title);
            Assert.Equal(128, firstResult.Runtime);
            Assert.Equal(2020, firstResult.Year);
        }

        [Fact]
        public void CanProvideResultMapping()
        {
            ResultMapper<MovieExtended>.CreateMap()
                .ForStringMember(p => p.Title)
                .ForIntegerMember(p => p.Runtime)
                .ForMember(p => p.Genre, r => r.ToString().Split(","))
                .ForMember(p => p.Released, r => DateTime.MinValue.AddSeconds((double)r));

            var searchResult = SearchResult.From(FakeSearchResult);

            var mappedResult = searchResult.As<MovieExtended>();

            var firstResult = mappedResult.First();

            Assert.Equal("Some movie that doesn't exist", firstResult.Title);
            Assert.Equal(128, firstResult.Runtime);
            Assert.Equal(new[] { "That thing", " this things", " that other thing" }, firstResult.Genre);
            Assert.Equal(new DateTime(2014, 11, 21), firstResult.Released);
        }

        private static RedisResult FakeSearchResult = RedisResult.Create(new[]
        {
            RedisResult.Create(1, ResultType.Integer),

            RedisResult.Create("this_is_the_document_key", ResultType.BulkString),

            RedisResult.Create(new[]
            {
                RedisResult.Create("Title", ResultType.BulkString),
                RedisResult.Create("Some movie that doesn't exist", ResultType.BulkString),

                RedisResult.Create("Runtime", ResultType.BulkString),
                RedisResult.Create("128", ResultType.BulkString),

                RedisResult.Create("Year", ResultType.BulkString),
                RedisResult.Create("2020", ResultType.BulkString),

                RedisResult.Create("Genre", ResultType.BulkString),
                RedisResult.Create("That thing, this things, that other thing", ResultType.BulkString),

                RedisResult.Create("Released", ResultType.BulkString),
                RedisResult.Create("63552124800", ResultType.BulkString),
            })
        });

        public class Movie
        {
            public string Title { get; set; }

            public int Runtime { get; set; }

            public int Year { get; set; }
        }

        public class MovieExtended
        {
            public string Title { get; set; }

            public int Runtime { get; set; }

            public string[] Genre { get; set; }

            public DateTime Released { get; set; }
        }
    }
}