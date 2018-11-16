﻿using System.Linq;
using System.Reflection;
using Stylet;
using Tyrrrz.Extensions;
using YoutubeDownloader.Models;
using YoutubeDownloader.Services;
using YoutubeDownloader.ViewModels.Components;
using YoutubeDownloader.ViewModels.Framework;

namespace YoutubeDownloader.ViewModels
{
    public class RootViewModel : Screen
    {
        private readonly IViewModelFactory _viewModelFactory;
        private readonly DialogManager _dialogManager;
        private readonly QueryService _queryService;

        public bool IsBusy { get; private set; }

        public string Query { get; set; }

        public BindableCollection<DownloadViewModel> Downloads { get; set; }

        public RootViewModel(IViewModelFactory viewModelFactory, DialogManager dialogManager, QueryService queryService)
        {
            _viewModelFactory = viewModelFactory;
            _dialogManager = dialogManager;
            _queryService = queryService;

            Downloads = new BindableCollection<DownloadViewModel>();

            // Title
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            DisplayName = $"YoutubeDownloader v{version}";
        }

        private void AddDownload(DownloadViewModel download)
        {
            // Find an existing download for this file path
            var existingDownload = Downloads.FirstOrDefault(d => d.FilePath == download.FilePath);

            // If it exists - cancel and remove it
            if (existingDownload != null)
            {
                if (existingDownload.CanCancel)
                    existingDownload.Cancel();

                Downloads.Remove(existingDownload);
            }

            // Add to the beginning
            Downloads.Insert(0, download);
        }

        public bool CanProcessQuery => !IsBusy && Query.IsNotBlank();

        public async void ProcessQuery()
        {
            IsBusy = true;

            // Execute query
            var executedQuery = await _queryService.ExecuteQueryAsync(Query);

            // If no videos were found - tell the user
            if (executedQuery.Videos.Count <= 0)
            {
                // Create dialog
                var dialog = _viewModelFactory.CreateMessageBoxViewModel();
                dialog.DisplayName = "Nothing found";
                dialog.Message = "Couldn't find any videos based on the query or URL you provided";

                // Show dialog
                await _dialogManager.ShowDialogAsync(dialog);
            }
            // If only one video was found - show download setup for single video
            else if (executedQuery.Videos.Count == 1)
            {
                // Create dialog
                var dialog = _viewModelFactory.CreateDownloadSingleSetupViewModel();
                dialog.DisplayName = executedQuery.Title;
                dialog.Video = executedQuery.Videos.Single();

                // Show dialog and get download
                var download = await _dialogManager.ShowDialogAsync(dialog);

                // Add download to the list (can be null if user canceled)
                if (download != null)
                    AddDownload(download);
            }
            // If multiple videos were found - show download setup for multiple videos
            else
            {
                // Create dialog
                var dialog = _viewModelFactory.CreateDownloadMultipleSetupViewModel();
                dialog.DisplayName = executedQuery.Title;
                dialog.AvailableVideos = executedQuery.Videos;

                // If this is a playlist - preselect all videos
                if (executedQuery.Query.Type == QueryType.Playlist)
                    dialog.SelectedVideos = dialog.AvailableVideos;

                // Show dialog and get downloads
                var downloads = await _dialogManager.ShowDialogAsync(dialog);

                // Add downloads to the list (can be null if user canceled)
                downloads?.ForEach(AddDownload);
            }

            IsBusy = false;
        }
    }
}