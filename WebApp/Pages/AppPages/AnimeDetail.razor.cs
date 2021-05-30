﻿using Commons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace WebApp.Pages.AppPages
{
    public partial class AnimeDetail
    {
        #region Injection

        [Inject] protected Generic Generic { get; set; }
        [Inject] protected SpinnerService Spinner { get; set; }
        [Inject] protected NavigationManager NavigationManager { get; set; }
        [Inject] protected UIConfiguration Theme { get; set; }
        [Inject] protected IJSRuntime Js { get; set; }

        #endregion

        #region Parameter

        [Parameter] public long AnimeID { get; set; }
        [Parameter] public int EpisodeNumber { get; set; }

        #endregion

        private Anime Anime { get; set; }
        private Anime Prequel { get; set; }
        private Anime Sequel { get; set; }
        
        private int EpisodesFrom { get; set; }
        private int EpisodesTo { get; set; }
        private Episode Episode { get; set; }

        private string AnimeTitle { get; set; }
        private string AnimeDescription { get; set; }

        private string PrequelTitle { get; set; }
        private string SequelTitle { get; set; }

        private string AnimeStartDate { get; set; }
        private string AnimeEndDate { get; set; }
        private string AnimeSeason { get; set; }

        protected override async Task OnInitializedAsync()
        {
            Spinner.Show();

            Anime = await GetAnime(AnimeID);

            if(Anime == null)
            {
                NavigationManager.NavigateTo("/404");
                return;
            }

            if(string.IsNullOrEmpty(Anime.TrailerUrl) && EpisodeNumber == 0)
            {
                EpisodeNumber = 1;
            }

            AnimeTitle = Anime.Titles.Keys.Contains(Theme.Locale) ? Anime.Titles[Theme.Locale] : Anime.Titles[Theme.FallbackLocale];
            AnimeDescription = Anime.Descriptions.Keys.Contains(Theme.Locale) ? Anime.Descriptions[Theme.Locale] : Anime.Descriptions[Theme.FallbackLocale];

            if (Anime.Prequel != null)
            {
                Prequel = await GetAnime(Anime.Prequel.Value);
                PrequelTitle = Prequel.Titles.Keys.Contains(Theme.Locale) ? Prequel.Titles[Theme.Locale] : Prequel.Titles[Theme.FallbackLocale];
            }

            if (Anime.Sequel != null)
            {
                Sequel = await GetAnime(Anime.Sequel.Value);
                SequelTitle = Sequel.Titles.Keys.Contains(Theme.Locale) ? Sequel.Titles[Theme.Locale] : Sequel.Titles[Theme.FallbackLocale];
            }

            AnimeStartDate = Anime.StartDate != null ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Anime.StartDate.Value.ToString("MMM dd, yyyy")) : "Unknown";
            AnimeEndDate = Anime.EndDate != null ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Anime.EndDate.Value.ToString("MMM dd, yyyy")) : "Unknown";
            AnimeSeason = Anime.SeasonYear != null ? $"{Theme.GetTextByAnimeSeason(Anime.SeasonPeriod)} {Anime.SeasonYear}" : "Unknown";

            if(EpisodeNumber % 120 == 0)
            {
                EpisodesFrom = EpisodeNumber - 119;
            }
            else
            {
                EpisodesFrom = EpisodeNumber - (EpisodeNumber % 120 - 1);
            }

            EpisodesTo = EpisodesFrom + 119;

            if(EpisodesTo > Anime.EpisodesCount)
            {
                EpisodesTo = Anime.EpisodesCount;
            }

            Spinner.Hide();
        }

        protected override void OnParametersSet()
        {
            if (Anime != null && string.IsNullOrEmpty(Anime.TrailerUrl) && EpisodeNumber == 0)
            {
                EpisodeNumber = 1;
            }

            base.OnParametersSet();
        }

        private async Task<Anime> GetAnime(long id)
        {
            APIResponse response = await Generic.GetSingleRequest<Anime>($"anime/{id}");
            return (Anime)response.Data;
        }

        private void OnEpisodeChanged(int episodeNumber)
        {
            EpisodeNumber = episodeNumber;
            Js.InvokeAsync<string>("App.ChangeURL", $"/App/Anime/{AnimeID}/{EpisodeNumber}");

            StateHasChanged();
        }
    }
}