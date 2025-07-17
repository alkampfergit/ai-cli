using AiCli.Application;
using AiCli.Configuration;
using AiCli.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Globalization;

namespace AiCli.Infrastructure;

/// <summary>
/// Service for managing configuration through interactive menus
/// </summary>
public class ConfigurationService
{
    private readonly IUserSettingsService _userSettingsService;
    private readonly ILogger<ConfigurationService> _logger;

    /// <summary>
    /// Initializes a new instance of the ConfigurationService
    /// </summary>
    /// <param name="userSettingsService">User settings service</param>
    /// <param name="logger">Logger instance</param>
    public ConfigurationService(IUserSettingsService userSettingsService, ILogger<ConfigurationService> logger)
    {
        _userSettingsService = userSettingsService;
        _logger = logger;
    }

    /// <summary>
    /// Starts the interactive configuration menu
    /// </summary>
    public async Task StartConfigurationAsync()
    {
        AnsiConsole.Write(new FigletText("AI CLI Config").Centered().Color(Color.Blue));
        AnsiConsole.WriteLine();

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to configure?")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "Add Model Configuration",
                        "Remove Model Configuration",
                        "List Model Configurations",
                        "Set Default Model Configuration",
                        "Exit"
                    }));

            switch (choice)
            {
                case "Add Model Configuration":
                    await AddModelConfigurationAsync();
                    break;
                case "Remove Model Configuration":
                    await RemoveModelConfigurationAsync();
                    break;
                case "List Model Configurations":
                    await ListModelConfigurationsAsync();
                    break;
                case "Set Default Model Configuration":
                    await SetDefaultModelConfigurationAsync();
                    break;
                case "Exit":
                    AnsiConsole.MarkupLine("[green]Configuration saved successfully![/]");
                    return;
            }

            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// Adds a new model configuration
    /// </summary>
    private async Task AddModelConfigurationAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Adding new model configuration[/]");
        AnsiConsole.WriteLine();

        var id = await AnsiConsole.AskAsync<string>("Enter configuration [green]ID[/]:");
        
        // Check if ID already exists
        var settings = _userSettingsService.Load();
        if (settings.GetModelConfiguration(id) != null)
        {
            AnsiConsole.MarkupLine("[red]Configuration with ID '{0}' already exists![/]", id);
            return;
        }

        var name = await AnsiConsole.AskAsync<string>("Enter configuration [green]name[/]:");
        var apiKey = await AnsiConsole.PromptAsync(new TextPrompt<string>("Enter [green]API key[/]:").Secret());
        var baseUrl = await AnsiConsole.AskAsync<string>("Enter [green]base URL[/] (or press Enter for default):");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = null;
        }

        var model = await  AnsiConsole.AskAsync<string>("Enter [green]model name[/]:", "gpt-3.5-turbo");
        
        var temperature = await AnsiConsole.AskAsync<float>("Enter [green]temperature[/] (0.0-2.0):", 1.0f);
        if (temperature < 0.0f || temperature > 2.0f)
        {
            AnsiConsole.MarkupLine("[red]Temperature must be between 0.0 and 2.0. Using default value 1.0.[/]");
            temperature = 1.0f;
        }

        var maxTokensInput = await AnsiConsole.AskAsync<string>("Enter [green]max tokens[/] (or press Enter for unlimited):");
        int? maxTokens = null;
        if (!string.IsNullOrWhiteSpace(maxTokensInput) && int.TryParse(maxTokensInput, out var parsedMaxTokens))
        {
            maxTokens = parsedMaxTokens;
        }

        var format = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select output [green]format[/]:")
                .AddChoices(new[] { "text", "json" })
                .WrapAround()
        );

        var stream = AnsiConsole.Confirm("Enable [green]streaming[/] by default?", false);

        var modelConfig = new ModelConfiguration
        {
            Id = id,
            Name = name,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            Model = model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Format = format,
            Stream = stream
        };

        settings.AddOrUpdateModelConfiguration(modelConfig);
        _userSettingsService.Save(settings);

        AnsiConsole.MarkupLine("[green]Model configuration '{0}' added successfully![/]", name);
        _logger.LogInformation("Added model configuration: {ConfigId}", id);
    }

    /// <summary>
    /// Removes a model configuration
    /// </summary>
    private async Task RemoveModelConfigurationAsync()
    {
        var settings = _userSettingsService.Load();
        
        if (settings.ModelConfigurations.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No model configurations found![/]");
            return;
        }

        if (settings.ModelConfigurations.Count == 1)
        {
            AnsiConsole.MarkupLine("[red]Cannot remove the only model configuration![/]");
            return;
        }

        var choices = settings.ModelConfigurations.Select(m => $"{m.Name} ({m.Id})").ToArray();
        var selected = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select model configuration to [red]remove[/]:")
                .AddChoices(choices)
                .WrapAround()
        );

        // Extract ID from selection
        var selectedId = selected.Split('(').Last().TrimEnd(')');
        var configToRemove = settings.GetModelConfiguration(selectedId);

        if (configToRemove == null)
        {
            AnsiConsole.MarkupLine("[red]Configuration not found![/]");
            return;
        }

        var confirm = AnsiConsole.Confirm($"Are you sure you want to remove '[red]{configToRemove.Name}[/]'?");
        if (!confirm)
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
            return;
        }

        settings.ModelConfigurations.Remove(configToRemove);

        // If we removed the default configuration, set a new default
        if (settings.DefaultModelConfigurationId == configToRemove.Id)
        {
            settings.DefaultModelConfigurationId = settings.ModelConfigurations.First().Id;
            AnsiConsole.MarkupLine("[yellow]Default model configuration changed to: {0}[/]", settings.ModelConfigurations.First().Name);
        }

        _userSettingsService.Save(settings);

        AnsiConsole.MarkupLine("[green]Model configuration '{0}' removed successfully![/]", configToRemove.Name);
        _logger.LogInformation("Removed model configuration: {ConfigId}", configToRemove.Id);
    }

    /// <summary>
    /// Lists all model configurations
    /// </summary>
    private Task ListModelConfigurationsAsync()
    {
        var settings = _userSettingsService.Load();
        
        if (settings.ModelConfigurations.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No model configurations found![/]");
            return Task.CompletedTask;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Model");
        table.AddColumn("Base URL");
        table.AddColumn("Default");

        foreach (var config in settings.ModelConfigurations)
        {
            var isDefault = config.Id == settings.DefaultModelConfigurationId;
            table.AddRow(
                config.Id,
                config.Name,
                config.Model,
                config.BaseUrl ?? "Default",
                isDefault ? "[green]Yes[/]" : "No"
            );
        }

        AnsiConsole.Write(table);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the default model configuration
    /// </summary>
    private async Task SetDefaultModelConfigurationAsync()
    {
        var settings = _userSettingsService.Load();
        
        if (settings.ModelConfigurations.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No model configurations found![/]");
            return;
        }

        if (settings.ModelConfigurations.Count == 1)
        {
            AnsiConsole.MarkupLine("[yellow]Only one model configuration exists. It is already the default.[/]");
            return;
        }

        var choices = settings.ModelConfigurations.Select(m => $"{m.Name} ({m.Id})").ToArray();
        var selected = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]default[/] model configuration:")
                .AddChoices(choices)
                .WrapAround()
        );

        // Extract ID from selection
        var selectedId = selected.Split('(').Last().TrimEnd(')');
        var selectedConfig = settings.GetModelConfiguration(selectedId);

        if (selectedConfig == null)
        {
            AnsiConsole.MarkupLine("[red]Configuration not found![/]");
            return;
        }

        settings.DefaultModelConfigurationId = selectedConfig.Id;
        _userSettingsService.Save(settings);

        AnsiConsole.MarkupLine("[green]Default model configuration set to: {0}[/]", selectedConfig.Name);
        _logger.LogInformation("Set default model configuration: {ConfigId}", selectedConfig.Id);
    }
}