using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualBasic;
using ModernWindow.Essentials;
using ModernWindow.Interface.Widgets;
using ModernWindow.PackageEngine;
using ModernWindow.Structures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ModernWindow.Interface
{

    public partial class DiscoverPackagesPage : Page
    {
        public ObservableCollection<Package> Packages = new ObservableCollection<Package>();
        public SortableObservableCollection<Package> FilteredPackages = new SortableObservableCollection<Package>() { SortingSelector = (a) => (a.Name)};
        protected ObservableCollection<ManagerSource> UsedSources = new ObservableCollection<ManagerSource>();
        protected MainAppBindings bindings = MainAppBindings.Instance;

        protected TranslatedTextBlock MainTitle;
        protected TranslatedTextBlock MainSubtitle;
        protected ListView PackageList;
        protected ProgressBar LoadingProgressBar;
        protected Image HeaderImage;
        protected MenuFlyout ContextMenu;

        private bool IsDescending = true;

        private bool Initialized = false;

        public string InstantSearchSettingString = "DisableInstantSearchOnDiscover";
        public DiscoverPackagesPage()
        {
            this.InitializeComponent();
            MainTitle = __main_title;
            MainSubtitle = __main_subtitle;
            PackageList = __package_list;
            HeaderImage = __header_image;
            LoadingProgressBar = __loading_progressbar;
            Initialized = true;
            ReloadButton.Click += async (s, e) => { await __load_packages(); } ;
            FindButton.Click += async (s, e) => { await FilterPackages(QueryBlock.Text); };
            QueryBlock.TextChanged += async (s, e) => { if (InstantSearchCheckbox.IsChecked == true) await FilterPackages(QueryBlock.Text); };
            QueryBlock.KeyUp += async (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) await FilterPackages(QueryBlock.Text); };
            PackageList.ItemClick += (s, e) => { if (e.ClickedItem != null) Console.WriteLine("Clicked item " + (e.ClickedItem as Package).Id); };
            GenerateToolBar();
            LoadInterface();
            _ = __load_packages();
        }

        protected async Task __load_packages()
        {
            if (!Initialized)
                return;
            MainSubtitle.Text= "Loading...";
            LoadingProgressBar.Visibility = Visibility.Visible;
            //await this.LoadPackages();
            await this.FilterPackages(QueryBlock.Text);
            MainSubtitle.Text = "Found packages: " + Packages.Count().ToString();
            LoadingProgressBar.Visibility = Visibility.Collapsed;
        }

        protected void AddPackageToSourcesList(Package package)
        {
            if (!Initialized)
                return;
        }

        /*
         * 
         * 
         *  DO NOT MODIFY THE UPPER PART OF THIS FILE
         * 
         * 
         */

        public async Task LoadPackages()
        {
            if (!Initialized)
                return;
            MainSubtitle.Text = "Loading...";
            LoadingProgressBar.Visibility = Visibility.Visible;
            var intialQuery = QueryBlock.Text;
            Packages.Clear();
            FilteredPackages.Clear();
            UsedSources.Clear();
            if (QueryBlock.Text == null || QueryBlock.Text.Length < 3)
            {
                MainSubtitle.Text = "Found packages: " + Packages.Count().ToString();
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                return;
            }
            
            if (intialQuery != QueryBlock.Text)
                return;

            var tasks = new List<Task<Package[]>>();

            foreach(var manager in bindings.App.PackageManagerList)
            {
                if(manager.IsEnabled() && manager.Status.Found)
                {
                    var task = manager.FindPackages(QueryBlock.Text);
                    tasks.Add(task);
                }
            }

            foreach(var task in tasks)
            {
                if (!task.IsCompleted)
                    await task;
                foreach (Package package in task.Result)
                {
                    if (intialQuery != QueryBlock.Text)
                        return;
                    Packages.Add(package);
                    AddPackageToSourcesList(package);
                }
            }
            
            MainSubtitle.Text = "Found packages: " + Packages.Count().ToString();
            LoadingProgressBar.Visibility = Visibility.Collapsed;
        }

        public async Task FilterPackages(string query)
        {
            if (!Initialized)
                return;
            await LoadPackages();
            FilterPackages_SortOnly(query);
        }

        public void FilterPackages_SortOnly(string query)
        {
            if (!Initialized)
                return;
            FilteredPackages.Clear();
            Package[] MatchingList;

            Func<string, string> CaseFunc;
            if (UpperLowerCaseCheckbox.IsChecked == true)
                CaseFunc = (x) => { return x; };
            else
                CaseFunc = (x) => { return x.ToLower(); };

            Func<string, string> CharsFunc;
            if (IgnoreSpecialCharsCheckbox.IsChecked == true)
                CharsFunc = (x) => { 
                    var temp_x = CaseFunc(x).Replace("-", "").Replace("_", "").Replace(" ", "").Replace("@", "").Replace("\t", "");
                    foreach(var entry in new Dictionary<char, string>
                        {
                            {'a', ""},
                            {'e', ""},
                            {'i', ""},
                            {'o', ""},
                            {'u', ""},
                            {'c', ""},
                            {'ñ', ""},
                        })
                    {
                        foreach(char InvalidChar in entry.Value)
                            x = x.Replace(InvalidChar, entry.Key);
                    }
                    return temp_x;
                };
            else
                CharsFunc = (x) => { return CaseFunc(x); };

            if (QueryIdRadio.IsChecked == true)
                MatchingList = Packages.Where(x => CharsFunc(x.Name).Contains(CharsFunc(query))).ToArray();
            else if (QueryNameRadio.IsChecked == true)
                MatchingList = Packages.Where(x => CharsFunc(x.Id).Contains(CharsFunc(query))).ToArray();
            else if (QueryBothRadio.IsChecked == true)
                MatchingList = Packages.Where(x => CharsFunc(x.Name).Contains(CharsFunc(query)) | CharsFunc(x.Id).Contains(CharsFunc(query))).ToArray();
            else // QuerySimilarResultsRadio == true
                MatchingList = Packages.ToArray();

            foreach (var match in MatchingList)
            {
                FilteredPackages.Add(match);
            }
        }

        public void SortPackages(string Sorter)
        {
            if (!Initialized)
                return;
            FilteredPackages.Descending = !FilteredPackages.Descending;
            FilteredPackages.SortingSelector = (a) => (a.GetType().GetProperty(Sorter).GetValue(a));
            var Item = PackageList.SelectedItem;
            FilteredPackages.Sort();
            if (Item != null)
                PackageList.SelectedItem = Item;
                PackageList.ScrollIntoView(Item);
        }

        public void LoadInterface()
        {
            if (!Initialized)
                return;
            MainTitle.Text = "Discover Packages";
            HeaderImage.Source = new BitmapImage(new Uri("ms-appx:///wingetui/resources/desktop_download.png"));
            CheckboxHeader.Content = " ";
            NameHeader.Content = bindings.Translate("Package Name");
            IdHeader.Content = bindings.Translate("Package ID");
            VersionHeader.Content = bindings.Translate("Version");
            // NewVersionHeader.Content = bindings.Translate("New version");
            SourceHeader.Content = bindings.Translate("Source");

            CheckboxHeader.Click += (s, e) => { SortPackages("IsCheckedAsString"); };
            NameHeader.Click += (s, e) => { SortPackages("Name"); };
            IdHeader.Click += (s, e) => { SortPackages("Id"); };
            VersionHeader.Click += (s, e) => { SortPackages("VersionAsFloat"); };
            // NewVersionHeader.Click += (s, e) => { SortPackages("NewVersionAsFloat"); };
            SourceHeader.Click += (s, e) => { SortPackages("SourceAsString"); };
        }


        public void GenerateToolBar()
        {
            if (!Initialized)
                return;
            var InstallSelected = new AppBarButton();
            var InstallAsAdmin = new AppBarButton();
            var InstallSkipHash = new AppBarButton();
            var InstallInteractive = new AppBarButton();

            var PackageDetails = new AppBarButton();
            var SharePackage = new AppBarButton();

            var SelectAll = new AppBarButton();
            var SelectNone = new AppBarButton();

            var ImportPackages = new AppBarButton();
            var ExportSelection = new AppBarButton();

            var HelpButton = new AppBarButton();

            ToolBar.PrimaryCommands.Add(InstallSelected);
            ToolBar.PrimaryCommands.Add(InstallAsAdmin);
            ToolBar.PrimaryCommands.Add(InstallSkipHash);
            ToolBar.PrimaryCommands.Add(InstallInteractive);
            ToolBar.PrimaryCommands.Add(new AppBarSeparator());
            ToolBar.PrimaryCommands.Add(PackageDetails);
            ToolBar.PrimaryCommands.Add(SharePackage);
            ToolBar.PrimaryCommands.Add(new AppBarSeparator());
            ToolBar.PrimaryCommands.Add(SelectAll);
            ToolBar.PrimaryCommands.Add(SelectNone);
            ToolBar.PrimaryCommands.Add(new AppBarSeparator());
            ToolBar.PrimaryCommands.Add(ImportPackages);
            ToolBar.PrimaryCommands.Add(ExportSelection);
            ToolBar.PrimaryCommands.Add(new AppBarSeparator());
            ToolBar.PrimaryCommands.Add(HelpButton);

            var Labels = new Dictionary<AppBarButton, string>
            { // Entries with a trailing space are collapsed
              // Their texts will be used as the tooltip
                { InstallSelected,      "Install Selected packages" },
                { InstallAsAdmin,       " Install as administrator" },
                { InstallSkipHash,      " Skip integrity checks" },
                { InstallInteractive,   " InteractiveInstallation" },
                { PackageDetails,       " Package details" },
                { SharePackage,         " Share" },
                { SelectAll,            " Select all" },
                { SelectNone,           " Clear selection" },
                { ImportPackages,       "Import packages" },
                { ExportSelection,      "Export selected packages" },
                { HelpButton,           "Help" }
            };

            foreach(var toolButton in Labels.Keys)
            {
                toolButton.IsCompact = Labels[toolButton][0] == ' ';
                if(toolButton.IsCompact)
                    toolButton.LabelPosition = CommandBarLabelPosition.Collapsed;
                toolButton.Label = bindings.Translate(Labels[toolButton].Trim());
            }

            var Icons = new Dictionary<AppBarButton, string>
            {
                { InstallSelected,      "install" },
                { InstallAsAdmin,       "runasadmin" },
                { InstallSkipHash,      "checksum" },
                { InstallInteractive,   "interactive" },
                { PackageDetails,       "info" },
                { SharePackage,         "share" },
                { SelectAll,            "selectall" },
                { SelectNone,           "selectnone" },
                { ImportPackages,       "import" },
                { ExportSelection,      "export" },
                { HelpButton,           "help" }
            };

            foreach (var toolButton in Icons.Keys)
                toolButton.Icon = new LocalIcon(Icons[toolButton]);

            InstallSelected.IsEnabled = false;
            InstallAsAdmin.IsEnabled = false;
            InstallSkipHash.IsEnabled = false;
            InstallInteractive.IsEnabled = false;
            PackageDetails.IsEnabled = false;
            ImportPackages.IsEnabled = false;
            ExportSelection.IsEnabled = false;
            HelpButton.IsEnabled = false;

            SharePackage.Click += (s, e) => { bindings.App.mainWindow.SharePackage(PackageList.SelectedItem as Package); };

            SelectAll.Click += (s, e) => { foreach (var package in FilteredPackages) package.IsChecked = true; FilterPackages_SortOnly(QueryBlock.Text); };
            SelectNone.Click += (s, e) => { foreach (var package in FilteredPackages) package.IsChecked = false; FilterPackages_SortOnly(QueryBlock.Text); };

        }
        private void MenuDetails_Invoked(object sender, Package package)
        {
            if (!Initialized)
                return;
        }

        private void MenuShare_Invoked(object sender, Package package)
        {
            if (!Initialized)
                return;
            bindings.App.mainWindow.SharePackage(package);
        }

        private void MenuInstall_Invoked(object sender, Package package)
        {
            if (!Initialized)
                return;
        }

        private void MenuSkipHash_Invoked(object sender, Package package)
        {
            if (!Initialized)
                return;
        }

        private void MenuInteractive_Invoked(object sender, Package package)
        {
            if (!Initialized)
                return;
        }

        private void MenuAsAdmin_Invoked(object sender, Package package)
        {
            if (!Initialized)
                return;
        }

        private void PackageContextMenu_AboutToShow(object sender, Package package)
        {
            if (!Initialized)
                return;
            PackageList.SelectedItem = package;
        }


        private void FilterOptionsChanged(object sender, RoutedEventArgs e)
        {
            if (!Initialized)
                return;
            FilterPackages_SortOnly(QueryBlock.Text);
        }

        private void InstantSearchValueChanged(object sender, RoutedEventArgs e)
        {
            if (!Initialized)
                return;
            bindings.SetSettings(InstantSearchSettingString, InstantSearchCheckbox.IsChecked == false);
        }   
    }
}