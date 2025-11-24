using Microsoft.Maui.Storage;
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
                new LinqToXmlStrategy()
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
            if (string.IsNullOrEmpty(_xmlContent)) return;

            try
            {
                string xmlData;
                if (AttributeTypePicker.SelectedItem != null && AttributeValuePicker.SelectedItem != null)
                {
                    var criteria = new Dictionary<string, string> {
                       { _uiToXmlKeys[AttributeTypePicker.SelectedItem.ToString()], AttributeValuePicker.SelectedItem.ToString() }
                   };
                    xmlData = _currentStrategy.Search(_xmlContent, criteria);
                }
                else
                {
                    xmlData = _currentStrategy.Search(_xmlContent, new Dictionary<string, string>());
                }
                var transformer = new XslTransformer();
                string htmlContent = transformer.XmltoHTML(xmlData, _xslContent);
                string fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                string targetFile = Path.Combine(FileSystem.Current.CacheDirectory, fileName);

                File.WriteAllText(targetFile, htmlContent);
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Зберегти HTML звіт",
                    File = new ShareFile(targetFile)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка експорту", ex.Message, "OK");
            }
        }
        private void ResetFilters_Clicked(object sender, EventArgs e)
        {
            AttributeTypePicker.SelectedIndex = -1;
            AttributeValuePicker.ItemsSource = null;
            PerformSearch(new Dictionary<string, string>());
        }
        private void SearchButton_Clicked(object sender, EventArgs e)
        {
            if (AttributeTypePicker.SelectedItem == null || AttributeValuePicker.SelectedItem == null)
            {
                DisplayAlert("Увага", "Оберіть критерій та значення", "OK");
                return;
            }

            var criteria = new Dictionary<string, string>();
            string uiKey = AttributeTypePicker.SelectedItem.ToString();
            string value = AttributeValuePicker.SelectedItem.ToString();
            criteria.Add(_uiToXmlKeys[uiKey], value);

            PerformSearch(criteria);
        }

        private void PerformSearch(Dictionary<string, string> criteria)
        {
            if (string.IsNullOrEmpty(_xmlContent)) return;
            try
            {
                var resultsXml = _currentStrategy.Search(_xmlContent, criteria);
                if (!resultsXml.Contains("<Scientist"))
                    DisplayAlert("Результат", "Нічого не знайдено.", "OK");
                else
                    UpdateWebView(resultsXml);
            }
            catch (Exception ex) { DisplayAlert("Помилка", ex.Message, "OK"); }
        }
        private void LoadFilterData()
        {
            if (string.IsNullOrEmpty(_xmlContent) || _currentStrategy == null) return;
            _filterCache = _currentStrategy.GetFilterAttributes(_xmlContent);
            AttributeTypePicker.ItemsSource = _uiToXmlKeys.Keys.ToList();
        }

        private void AttributeTypePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (AttributeTypePicker.SelectedItem == null || _filterCache == null) return;
            string selectedUiKey = AttributeTypePicker.SelectedItem.ToString();
            string xmlKey = _uiToXmlKeys[selectedUiKey];
            if (_filterCache.TryGetValue(xmlKey, out var values))
            {
                AttributeValuePicker.ItemsSource = values;
                AttributeValuePicker.SelectedIndex = -1;
                AttributeValuePicker.Title = $"Оберіть {selectedUiKey.ToLower()}";
            }
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