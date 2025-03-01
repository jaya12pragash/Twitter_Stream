﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twitter_Stream.Entities;
using Twitter_Stream.Models;
using Twitter_Stream.Services;

namespace Twitter_Stream
{
    class Program
    {
        static AppSettings SETTINGS;
    
        static readonly string CONFIG_FILE_PATH = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "../../../", "Data", @"appsettings.json");
        static SearchService searchService;

        static void LoadConfigurations()
        {
            if (File.Exists(CONFIG_FILE_PATH))
                SETTINGS = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(CONFIG_FILE_PATH));
            else
                throw new Exception("Application settings file couldn't be found");
        }

        static List<Status> QueryTweets(string query)
        {
            var userStatusCollection = new List<Status>();
            var userStatusQuery = searchService.GetData(query).Result;
            var userStatusObj = JsonConvert.DeserializeObject<TweetSearchResponse>(userStatusQuery);
            userStatusCollection.AddRange(userStatusObj.Statuses);

            if (string.IsNullOrEmpty(userStatusObj.SearchMetadata.NextResults))
                return userStatusCollection;
            else
                userStatusCollection.AddRange(QueryTweets($"{userStatusObj.SearchMetadata.NextResults}&tweet_mode=extended"));

            return userStatusCollection;
        }

        private static List<Tweet> ConvertToEntity(List<Status> status)
        {
            var formattedReplyData = status.Select(x => new Tweet
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                FullText = x.FullText,
                RetweetCount = x.RetweetCount,
                FavoriteCount = x.FavoriteCount,
                InReplyToStatusId = x.InReplyToStatusId,
                UserId = x.User.Id,
                UserName = x.User.Name,
                UserScreenName = x.User.ScreenName,
                UserLocation = x.User.Location,
                UserFollowersCount = x.User.FollowersCount,
                UserFriendsCount = x.User.FriendsCount,
                UserCreatedAt = x.User.CreatedAt,
                UserFavouritesCount = x.User.FavouritesCount,
                UserStatusesCount = x.User.StatusesCount,
                TweetText = JsonConvert.SerializeObject(x)
            }).ToList();

            return formattedReplyData;
        }

        static void Main(string[] args)
        {
            LoadConfigurations();
            searchService = new SearchService(SETTINGS.AuthorizeToken);
            var dbService = new DbService();

            // GET LAST SAVED-IN-DB STATUS UPDATE FOR MAIN USER
            var lastTwitterIdSaved = dbService.GetLastSavedTweetId(SETTINGS.MainUserId);
            var maxTwitterIdRetrieved = lastTwitterIdSaved;
            var newTweetIds = new List<long> { };
            // GET ALL STATUS UPDATES FOR SOME USER SINCE LAST REGISTERED STATUS UPDATE
            var query = $"?q=from:{SETTINGS.MainUserName}&tweet_mode=extended&count=100";

            if (lastTwitterIdSaved != 0)
                query += $"&since_id={lastTwitterIdSaved}";

            var mainUserTweets = QueryTweets(query);

            if (mainUserTweets.Count > 0)
            {
                var mainUserParsedTweets = ConvertToEntity(mainUserTweets);
                dbService.AddTweets(mainUserParsedTweets);

                if (lastTwitterIdSaved != 0)
                {
                    newTweetIds.AddRange(mainUserParsedTweets.Select(x => x.Id).ToList());
                    maxTwitterIdRetrieved = newTweetIds.Max();
                }
                else
                {
                    newTweetIds = mainUserParsedTweets.Select(x => x.Id).ToList();
                }
            }

            query = $"?q=to:{SETTINGS.MainUserName}&tweet_mode=extended&count=100";
            if (lastTwitterIdSaved != 0)
                query += $"&since_id={lastTwitterIdSaved}";

            if (maxTwitterIdRetrieved.HasValue && maxTwitterIdRetrieved != lastTwitterIdSaved)
                query += $"&max_id={maxTwitterIdRetrieved}";

            var repliedTweets = QueryTweets(query);

            // filter replies, keep only direct replies to main user
            repliedTweets = repliedTweets.Where(x => x.InReplyToStatusId.HasValue && newTweetIds.Contains(x.InReplyToStatusId.Value)).ToList();
            var repliedTweetsParsed = ConvertToEntity(repliedTweets);
            var items = dbService.AddTweets(repliedTweetsParsed);
            Console.WriteLine($"Inserted {items} items");

            Console.ReadLine();
        }
    }
}

