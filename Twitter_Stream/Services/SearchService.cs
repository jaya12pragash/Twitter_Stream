using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Twitter_Stream.Services
{
    public class SearchService
    {
        private readonly HttpClient _client;
        public SearchService(string bearerToken)
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri("https://api.twitter.com/1.1/search/tweets.json"),
            };
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
        }

        public async Task<string> GetData(string q)
        {
            var result = await _client.GetStringAsync(q);
            return result;
        }
    }
}
