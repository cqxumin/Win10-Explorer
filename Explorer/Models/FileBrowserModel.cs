﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using FileAttributes = Windows.Storage.FileAttributes;

namespace Explorer.Models
{
    public class FileBrowserModel : BaseModel
    {
        private static DataTransferManager dataTransferManager;
        private static DataPackage sharedData;

        private string path;
        private FileSystemElement currentFolder;

        private int historyPosition;

        public ObservableCollection<FileSystemElement> FileSystemElements { get; set; }
        public List<FileSystemElement> History { get; set; }
        public FileSystemElement SelectedElement { get; set; }


        public FileBrowserModel()
        {
            FileSystemElements = new ObservableCollection<FileSystemElement>();
            CurrentFolder = new FileSystemElement { Path = "C:", Name = "Local Disk"};

            NavigateBack = new Command(x => NavigateToNoHistory(History[--HistoryPosition]), () => HistoryPosition > 0);
            NavigateForward = new Command(x => NavigateToNoHistory(History[++HistoryPosition]), () => HistoryPosition < History.Count - 1);

            History = new List<FileSystemElement>();
            HistoryPosition = -1;

            //For sharing files, only one object for all fileBrowsers
            if (dataTransferManager == null)
            {
                dataTransferManager = DataTransferManager.GetForCurrentView();
                dataTransferManager.DataRequested += OnShareRequested;
            }

            NavigateTo(CurrentFolder);
        }

        public FileSystemElement CurrentFolder
        {
            get { return currentFolder; }
            private set { currentFolder = value; OnPropertyChanged(); OnPropertyChanged("Path"); OnPropertyChanged("Name"); }
        }

        public string Name
        {
            get { return currentFolder.Name; }
            set { currentFolder.Name = value; OnPropertyChanged(); }
        }

        public string Path
        {
            get { return path; }
            set { path = value; OnPropertyChanged(); }
        }

        public int HistoryPosition
        {
            get { return historyPosition; }
            set { historyPosition = value; OnPropertyChanged(); NavigateBack.CanExceuteChanged(); NavigateForward.CanExceuteChanged(); }
        }

        public Command NavigateBack { get; }

        public Command NavigateForward { get; }


        /// <summary>
        /// Navigate to the passed Folder
        /// </summary>
        /// <param name="fse"></param>
        public void NavigateTo(FileSystemElement fse)
        {
            Path = fse.Path;

            History.RemoveRange(HistoryPosition + 1, History.Count - (HistoryPosition + 1));
            History.Insert(HistoryPosition + 1, fse);
            HistoryPosition++;

            LoadFolderAsync(fse);
        }


        /// <summary>
        /// Navigate without history to the passed Folder (internal use e.g. nav buttons)
        /// </summary>
        /// <param name="fse"></param>
        private void NavigateToNoHistory(FileSystemElement fse)
        {
            Path = fse.Path;

            LoadFolderAsync(fse);
        }

        /// <summary>
        /// Launches an executable file
        /// </summary>
        /// <param name="path">The path to the executable</param>
        /// <param name="arguments">The launch arguments passed to the executable</param>
        public void LaunchExe(string path, string arguments = null)
        {
            FileSystem.LaunchExeAsync(path, arguments);
        }

        /// <summary>
        /// Loads the folder's contents
        /// </summary>
        private async void LoadFolderAsync(FileSystemElement fse)
        {
            try
            {
                var files = await Task.Run(() => FileSystem.GetFolderContentSimple(fse.Path));
                if (Path != fse.Path) return;

                FileSystemElements.Clear();
                foreach (var file in files)
                {
                    FileSystemElements.Add(file);
                }

                CurrentFolder = fse;

            }
            catch (Exception e)
            {
                //await Windows.System.Launcher.LaunchUriAsync(new Uri(“ms-settings:privacy-broadfilesystemaccess”));
                //Show error that path was not found
            }
        }

        /// <summary>
        /// Event called in UI that navigates to the passed FileSystemElement
        /// </summary>
        public void NavigateNextFSE(object sender, FileSystemElement fileSystemElement)
        {
            NavigateTo(fileSystemElement);
        }

        public void NavigateNavigationFSE(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var path = args.InvokedItemContainer.Tag.ToString();

            NavigateTo(new FileSystemElement { Path = path });
        }

        /// <summary>
        /// Called from system when DataTransferManager.ShowShareUI has been called
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnShareRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            sharedData.Properties.Title = "Share Text Example";
            sharedData.Properties.Description = "A demonstration that shows how to share text.";
            args.Request.Data = sharedData;
        }

        public void OpenFile()
        {
            if (SelectedElement.Type.HasFlag(FileAttributes.Directory)) NavigateTo(SelectedElement);
            else FileSystem.OpenFileWithDefaultApp(SelectedElement.Path);
        }

        public void OpenFileWith()
        {
            if (!SelectedElement.Type.HasFlag(FileAttributes.Directory)) FileSystem.OpenFileWith(SelectedElement.Path);
        }

        public async void ShareFile()
        {
            DataTransferManager.ShowShareUI();
            sharedData = new DataPackage();
            sharedData.SetStorageItems(new List<IStorageItem> { await FileSystem.GetStorageItemAsync(SelectedElement) });
        }

        public void RenameFile()
        {
            FileSystem.RenameStorageItem(SelectedElement, "sad");
        }

        public void DeleteStorageItem()
        {
            FileSystem.DeleteStorageItem(SelectedElement);
            FileSystemElements.Remove(SelectedElement);
            SelectedElement = null;
        }

        public async void ShowPropertiesOfFile()
        {
            var props = await FileSystem.GetPropertiesOfFile(SelectedElement.Path);
        }

        public void Refresh()
        {
            LoadFolderAsync(CurrentFolder);
        }
    }
}
