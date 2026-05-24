using GHKanban.AgentImage.Config;
using Microsoft.Extensions.AI;

namespace GHKanban.AgentImage.Providers;

/// <summary>Creates <see cref="IChatClient"/> instances from <see cref="SkillLlm"/> configuration.</summary>
/// <remarks>
/// Returns <see langword="null"/> for the <c>none</c> provider so callers can short-circuit without
/// making any network connections.
/// </remarks>
public static class ChatClientFactory
{
    /// <summary>Creates an <see cref="IChatClient"/> for the given LLM configuration, or <see langword="null"/> when the provider is <c>none</c>.</summary>
    /// <param name="cfg">The LLM provider/model settings.</param>
    /// <param name="apiKey">The API key for the chosen provider.</param>
    /// <returns>A configured <see cref="IChatClient"/>, or <see langword="null"/> for the <c>none</c> provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="cfg"/> specifies an unknown provider.</exception>
    public static IChatClient? Create(SkillLlm cfg, string apiKey) => cfg.Provider switch
    {
        "none"      => null,
        "anthropic" => CreateAnthropic(apiKey),
        "openai"    => CreateOpenAi(cfg, apiKey),
        _ => throw new InvalidOperationException($"Unknown llm.provider: {cfg.Provider}")
    };

    // Anthropic.SDK 5.x: AnthropicClient.Messages directly implements IChatClient.
    // The model is specified at call time via ChatOptions.ModelId, not at construction.
    private static IChatClient CreateAnthropic(string apiKey)
    {
        var anthropic = new Anthropic.SDK.AnthropicClient(apiKey);
        return anthropic.Messages;
    }

    // Microsoft.Extensions.AI.OpenAI 9.7.x: use OpenAI.Chat.ChatClient(model, apiKey)
    // and call the .AsIChatClient() extension method from Microsoft.Extensions.AI.OpenAI.
    private static IChatClient CreateOpenAi(SkillLlm cfg, string apiKey)
    {
        var chatClient = new OpenAI.Chat.ChatClient(cfg.Model ?? "gpt-4o", apiKey);
        return chatClient.AsIChatClient();
    }
}
