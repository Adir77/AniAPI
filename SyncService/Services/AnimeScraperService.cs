﻿using Models;
using Models.Collections;
using Newtonsoft.Json;
using SyncService.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace SyncService.Services
{
    public class AnimeScraperService : IService
    {
        #region Members

        private AnimeCollection _animeCollection = new AnimeCollection();
        private HttpClient _anilistClient = new HttpClient() { BaseAddress = new Uri("https://graphql.anilist.co") };
        private GraphQLQuery _anilistQuery = new GraphQLQuery()
        {
            Query = @"
            query($page: Int, $format: MediaFormat) {
                Page(page: $page, perPage: 50) {
                    pageInfo {
                        lastPage
                    }
                    media(format: $format) {
                        id
                        idMal
                        format
                        status
                        title {
                            romaji
                            native
                        }
                        description
                        startDate {
                            year
                            month
                            day
                        }
                        endDate {
                            year
                            month
                            day
                        }
                        season
                        seasonYear
                        episodes
                        duration
                        trailer {
                            id
                            site
                        }
                        coverImage {
                            large
                            color
                        }
                        bannerImage
                        genres
                        relations {
                            edges {
                                relationType
                                node {
                                    id
                                    format
                                }
                            }
                        }
                        nextAiringEpisode {
                            episode
                        }
                        averageScore
                    }
                }
            }
            ",
            Variables = new Dictionary<string, object>()
        };
        private int _totalPages = 1;
        private List<string> _formatsFilter = new List<string>() { "TV", "TV_SHORT", "MOVIE", "SPECIAL", "OVA", "ONA", "MUSIC" };
        private int _rateLimitRemaining;
        private long _rateLimitReset;

        protected override int TimeToWait => 60 * 1000 * 60 * 12; // 12 Hours

        #endregion
        
        protected override ServicesStatus GetServiceStatus()
        {
            return new ServicesStatus()
            {
                Name = "AnimeScraper",
                Status = ServiceStatusEnum.NONE
            };
        }

        public override void Start()
        {
            this._rateLimitRemaining = 90;
            this._rateLimitReset = DateTime.Now.Ticks;
            
            base.Start();
        }

        public override async void Work()
        {
            base.Work();

            try
            {
                foreach (string formatFilter in this._formatsFilter)
                {
                    this._anilistQuery.Variables["format"] = formatFilter;

                    for (int currentPage = 1; currentPage <= this._totalPages; currentPage++)
                    {
                        this._anilistQuery.Variables["page"] = currentPage;

                        var request = new HttpRequestMessage
                        {
                            Method = HttpMethod.Post,
                            Content = new StringContent(JsonConvert.SerializeObject(this._anilistQuery), Encoding.UTF8, "application/json")
                        };

                        try
                        {
                            using (var response = await this._anilistClient.SendAsync(request))
                            {
                                try
                                {
                                    response.EnsureSuccessStatusCode();
                                }
                                catch (Exception ex)
                                {
                                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                                    {
                                        this._rateLimitReset = Convert.ToInt64(((string[])response.Headers.GetValues("X-RateLimit-Reset"))[0]);
                                    }

                                    throw new HttpRequestException("RateLimit superato", ex);
                                }

                                AnilistResponse anilistResponse = JsonConvert.DeserializeObject<AnilistResponse>(await response.Content.ReadAsStringAsync());

                                if (currentPage == 1)
                                {
                                    this._totalPages = anilistResponse.Data.Page.PageInfo.LastPage;
                                }

                                foreach (AnilistResponse.ResponseMedia m in anilistResponse.Data.Page.Media)
                                {
                                    Anime a = new Anime(m);
                                    if (this._animeCollection.Exists(ref a))
                                    {
                                        this._animeCollection.Edit(ref a);
                                    }
                                    else
                                    {
                                        this._animeCollection.Add(ref a);
                                    }
                                }

                                this._rateLimitRemaining = Convert.ToInt32(((string[])response.Headers.GetValues("X-RateLimit-Remaining"))[0]);

                                this.Log($"Format {formatFilter} done {GetProgress(currentPage, this._totalPages)}%");
                            }
                        }
                        catch (HttpRequestException ex)
                        {
                            currentPage--;

                            DateTime timeOfReset = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            timeOfReset = timeOfReset.AddSeconds(this._rateLimitReset).ToLocalTime();

                            TimeSpan timeToWait = timeOfReset - DateTime.Now;

                            this.Log($"Waiting {timeToWait.TotalMilliseconds.ToString("F0")} ms!");

                            Thread.Sleep((int)timeToWait.TotalMilliseconds + 1000);
                        }
                    }
                }

                this.Wait();
            }
            catch
            {
                this.Stop();
            }
        }
    }
}