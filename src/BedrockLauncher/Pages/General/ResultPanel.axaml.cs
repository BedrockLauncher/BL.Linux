using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace BedrockLauncher.Pages.General
{
    public enum ResultPanelType { NoContent, NoNews, Loading, Error }

    public partial class ResultPanel : UserControl
    {
        public static readonly StyledProperty<ResultPanelType> PanelTypeProperty =
            AvaloniaProperty.Register<ResultPanel, ResultPanelType>(nameof(PanelType));

        public ResultPanelType PanelType
        {
            get => GetValue(PanelTypeProperty);
            set => SetValue(PanelTypeProperty, value);
        }

        public ResultPanel()
        {
            InitializeComponent();
            Update();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PanelTypeProperty) Update();
        }

        private void Update()
        {
            if (Text1 == null) return;
            (string title, string text) = PanelType switch
            {
                ResultPanelType.NoNews => ("Dialog_NoNewsContentFound_NoMatchingResultsTitle", "Dialog_NoNewsContentFound_NoMatchingResultsText"),
                ResultPanelType.Loading => ("Dialog_ResultPanel_Loading_Title", "Dialog_ResultPanel_Loading_Text"),
                ResultPanelType.Error => ("Dialog_ResultPanel_Error_Title", "Dialog_ResultPanel_Error_Text"),
                _ => ("Dialog_NoContentFound_NoMatchingResultsTitle", "Dialog_NoContentFound_NoMatchingResultsText"),
            };
            Text1.Bind(TextBlock.TextProperty, Text1.GetResourceObservable(title));
            Text2.Bind(TextBlock.TextProperty, Text2.GetResourceObservable(text));
        }
    }
}
