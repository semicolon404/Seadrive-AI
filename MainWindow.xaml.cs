using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives; // Required for Popup

namespace LMStudioCli
{
    public partial class MainWindow : Window
    {
        private HttpClient _httpClient = new HttpClient();
        private ObservableCollection<ChatMessage> _chatMessages = new ObservableCollection<ChatMessage>();

        public MainWindow()
        {
            InitializeComponent();
            ChatItemsControl.ItemsSource = _chatMessages;
        }

        private async void AskButton_Click(object sender, RoutedEventArgs e)
        {
            string question = QuestionTextBox.Text.Trim();
            if (string.IsNullOrEmpty(question)) return;

            _chatMessages.Add(new ChatMessage { Text = question, IsUser = true });
            QuestionTextBox.Clear();

            string apiAddress = Properties.Settings.Default.ApiAddress; // Assuming you have this in settings
            if (string.IsNullOrWhiteSpace(apiAddress))
            {
                _chatMessages.Add(new ChatMessage { Text = "Please set the AI API address in Settings.", IsUser = false });
                return;
            }

            try
            {
                string apiUrl = $"{apiAddress}/v1/chat/completions";
                var requestPayload = new
                {
                    messages = new[]
                    {
                        new { role = "user", content = question }
                    },
                    stream = false
                };
                var jsonPayload = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        if (choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("content", out var answer))
                        {
                            _chatMessages.Add(new ChatMessage { Text = answer.GetString(), IsUser = false });
                        }
                        else if (choices[0].TryGetProperty("text", out var answerText)) // For some simpler APIs
                        {
                            _chatMessages.Add(new ChatMessage { Text = answerText.GetString(), IsUser = false });
                        }
                        else
                        {
                            _chatMessages.Add(new ChatMessage { Text = "Error: Could not parse AI response (content).", IsUser = false });
                        }
                    }
                    else
                    {
                        _chatMessages.Add(new ChatMessage { Text = "Error: Could not parse AI response (choices).", IsUser = false });
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _chatMessages.Add(new ChatMessage { Text = $"Error communicating with AI: {ex.Message}", IsUser = false });
            }
            catch (TaskCanceledException)
            {
                _chatMessages.Add(new ChatMessage { Text = "Request to AI timed out.", IsUser = false });
            }
            catch (JsonException ex)
            {
                _chatMessages.Add(new ChatMessage { Text = $"Error parsing AI response: {ex.Message}", IsUser = false });
            }
            finally
            {
                // Optionally scroll to the latest message
                ChatItemsControl.Items.MoveCurrentToLast();
                if (ChatItemsControl.Items.CurrentPosition >= 0)
                    (ChatItemsControl.ItemContainerGenerator.ContainerFromItem(ChatItemsControl.Items.CurrentItem) as FrameworkElement)?.BringIntoView();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Find the IpAddressTextBox in the visual tree
            var settingsPopup = FindName("SettingsPopup") as Popup;
            if (settingsPopup != null && settingsPopup.Child is Border border && border.Child is StackPanel stackPanel && stackPanel.Children.Count > 1 && stackPanel.Children[1] is TextBox ipAddressTextBox)
            {
                // Save API address to settings when the application closes
                Properties.Settings.Default.ApiAddress = ipAddressTextBox.Text;
                Properties.Settings.Default.Save();
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsPopup = FindName("SettingsPopup") as Popup;
            if (settingsPopup != null)
            {
                settingsPopup.IsOpen = true;
            }
        }

        private void LogoButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsPopup = FindName("SettingsPopup") as Popup;
            if (settingsPopup != null)
            {
                settingsPopup.IsOpen = true;
            }
        }
    }

    public class ChatMessage
    {
        public string Text { get; set; }
        public bool IsUser { get; set; }
    }
}