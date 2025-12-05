using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using System.Text;
using System.Threading;

namespace OOP_LabWork2
{
    public partial class MainPage : ContentPage
    {
        private readonly List<IXml> _strategies;
        private IXml _currentStrategy;
        private string _xmlContent = string.Empty;
        private string _xslContent = string.Empty;
        private string _currentSearchResults = string.Empty;
        private Dictionary<string, List<string>> _filterCache;
        private readonly Dictionary<string, string> _uiToXmlKeys = new()
        {
            { "Факультет", "Faculty" },
            { "Кафедра", "Department" },
            { "Ступінь", "DegreeType" },
            { "Звання", "Rank" }
        };

        public MainPage()
        {
            InitializeComponent();
            _strategies = new List<IXml>
            {
                new Sax(),
                new Dom(),
                new LinqToXml()
            };

            StrategyPicker.ItemsSource = _strategies.Select(s => s.Name).ToList();
            if (_strategies.Any()) { StrategyPicker.SelectedIndex = 0; _currentStrategy = _strategies[0]; }
            LoadXsltTemplate();
        }
        private async void LoadXsltTemplate()
        {
            try
            {
                string filePath = "Files/Transform.xsl";
                bool exists = await FileSystem.AppPackageFileExistsAsync(filePath);
                if (!exists)
                {
                    filePath = "Transform.xsl";
                    exists = await FileSystem.AppPackageFileExistsAsync(filePath);
                }

                if (!exists)
                {
                    await DisplayAlert("Помилка", "Файл Transform.xsl не знайдено.", "OK");
                    return;
                }

                using var stream = await FileSystem.OpenAppPackageFileAsync(filePath);
                using var reader = new StreamReader(stream);
                _xslContent = await reader.ReadToEndAsync();
            }
            catch (Exception ex) { await DisplayAlert("Помилка XSLT", ex.Message, "OK"); }
        }
        private async void PickFileButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Оберіть XML файл",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                        { DevicePlatform.iOS, new[] { "public.xml" } },
                        { DevicePlatform.Android, new[] { "application/xml", "text/xml" } },
                        { DevicePlatform.WinUI, new[] { ".xml" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.xml" } }
                    })
                });

                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    using var reader = new StreamReader(stream);
                    _xmlContent = await reader.ReadToEndAsync();

                    FileNameLabel.Text = result.FileName;
                    FileNameLabel.TextColor = Color.FromArgb("#A6E3A1");

                    ToolbarGrid.IsEnabled = true;
                    ToolbarGrid.Opacity = 1;
                    LoadFilterData();
                    PerformSearch(new Dictionary<string, string>());
                }
            }
            catch (Exception ex) { await DisplayAlert("Помилка XML", ex.Message, "OK"); }
        }
        private async void ExportHtmlButton_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSearchResults))
            {
                await DisplayAlert("Увага", "Спочатку відкрийте файл або виконайте пошук.", "OK");
                return;
            }
            try
            {
                string xmlData = _currentSearchResults;
                var transformer = new XslTransformer();
                string htmlContent = transformer.XmltoHTML(xmlData, _xslContent);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
                string defaultFileName = $"FilteredReport_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                var fileSaverResult = await FileSaver.Default.SaveAsync(defaultFileName, stream, CancellationToken.None);
                if (fileSaverResult.IsSuccessful)
                {
                    await DisplayAlert("Успіх", $"Файл збережено: {fileSaverResult.FilePath}", "OK");
                }
                else
                {
                    if (fileSaverResult.Exception != null)
                        await DisplayAlert("Помилка", fileSaverResult.Exception.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка експорту", ex.Message, "OK");
            }
        }
        private void ResetFilters_Clicked(object sender, EventArgs e)
        {
            AttributeTypePicker.SelectedIndex = -1;
            SearchEntry.Text = string.Empty;
            PerformSearch(new Dictionary<string, string>());
        }
        private void SearchButton_Clicked(object sender, EventArgs e)
        {
            if (AttributeTypePicker.SelectedItem == null || string.IsNullOrWhiteSpace(SearchEntry.Text))
            {
                DisplayAlert("Увага", "Оберіть критерій та введіть текст для пошуку", "OK");
                return;
            }

            var criteria = new Dictionary<string, string>();
            string uiKey = AttributeTypePicker.SelectedItem.ToString();
            string value = SearchEntry.Text.Trim(); // Беремо текст, а не SelectedItem
            criteria.Add(_uiToXmlKeys[uiKey], value);

            PerformSearch(criteria);
        }
        private void PerformSearch(Dictionary<string, string> criteria)
        {
            if (string.IsNullOrEmpty(_xmlContent)) return;
            try
            {
                var resultsXml = _currentStrategy.Search(_xmlContent, criteria);
                _currentSearchResults = resultsXml;
                if (!resultsXml.Contains("<Scientist"))
                    DisplayAlert("Результат", "Нічого не знайдено.", "OK");
                else
                    UpdateWebView(resultsXml);
            }
            catch (Exception ex) { DisplayAlert("Помилка", ex.Message, "OK"); }
        }
        private void LoadFilterData()
        {
            AttributeTypePicker.ItemsSource = _uiToXmlKeys.Keys.ToList();
        }
        private void StrategyPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (StrategyPicker.SelectedIndex >= 0)
                _currentStrategy = _strategies[StrategyPicker.SelectedIndex];
        }

        private void UpdateWebView(string xmlData)
        {
            if (string.IsNullOrEmpty(_xslContent)) return;
            var transformer = new XslTransformer();
            var html = transformer.XmltoHTML(xmlData, _xslContent);
            ResultsWebView.Source = new HtmlWebViewSource { Html = html };
        }
        private async void ExitButton_Clicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert("Вихід", "Ви дійсно хочете вийти з програми?", "Так", "Ні");
            if (answer)
            {
                Application.Current.Quit();
            }
        }
        protected override bool OnBackButtonPressed()
        {
            Dispatcher.Dispatch(async () =>
            {
                bool answer = await DisplayAlert("Вихід", "Ви дійсно хочете вийти з програми?", "Так", "Ні");
                if (answer)
                {
                    Application.Current.Quit();
                }
            });
            return true;
        }
    }
}