using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBoard.Services;

namespace DevBoard.ViewModels;

public partial class AiViewModel : ObservableObject
{
    private OllamaService _ollama;

    [ObservableProperty]
    private string _userInput = "";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusText = "Проверка Ollama...";

    [ObservableProperty]
    private string _selectedModel = "qwen2.5-coder:3b";

    [ObservableProperty]
    private bool _isOllamaAvailable;

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();

    public AiViewModel()
    {
        _ollama = new OllamaService(SelectedModel);
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsOllamaAvailable = await _ollama.IsAvailableAsync();

        if (IsOllamaAvailable)
        {
            StatusText = "Подключено к Ollama";
            var models = await _ollama.GetModelsAsync();
            AvailableModels.Clear();
            foreach (var m in models)
                AvailableModels.Add(m.Name);

            if (!AvailableModels.Contains(SelectedModel) && AvailableModels.Count > 0)
            {
                SelectedModel = AvailableModels[0];
                _ollama.SetModel(SelectedModel);
            }
        }
        else
        {
            StatusText = "Ollama не найден";
            Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = "Ollama не запущен. Запустите Ollama и перезапустите раздел AI."
            });
        }

        OnPropertyChanged(nameof(AvailableModels));
    }

    partial void OnSelectedModelChanged(string value)
    {
        _ollama?.SetModel(value);
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsGenerating) return;
        if (!IsOllamaAvailable)
        {
            StatusText = "Ollama не доступен";
            return;
        }

        var userMessage = UserInput.Trim();
        UserInput = "";
        IsGenerating = true;
        StatusText = "Генерация...";

        Messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        try
        {
            var ollamaMessages = new List<OllamaMessage>();

            ollamaMessages.Add(new OllamaMessage
            {
                Role = "system",
                Content = "Ты — AI-ассистент в приложении DevBoard для разработчиков. " +
                          "Отвечай кратко и по делу на русском языке. " +
                          "Если тебя просят написать код — пиши готовый к использованию код. " +
                          "Не повторяй приветствия лишний раз."
            });

            foreach (var msg in Messages.Where(m => m.Role != "system"))
            {
                ollamaMessages.Add(new OllamaMessage
                {
                    Role = msg.Role,
                    Content = msg.Content
                });
            }

            var assistantMsg = new ChatMessage { Role = "assistant", Content = "" };
            Messages.Add(assistantMsg);

            await foreach (var chunk in _ollama.ChatStreamAsync(ollamaMessages))
            {
                assistantMsg.Content += chunk;

                var idx = Messages.IndexOf(assistantMsg);
                Messages.RemoveAt(idx);
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = assistantMsg.Content
                });
            }

            StatusText = $"Готово · {SelectedModel}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка";
            Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = $"Ошибка: {ex.Message}"
            });
        }
        finally
        {
            IsGenerating = false;
        }
    }
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsSystem => Role == "system";
}
